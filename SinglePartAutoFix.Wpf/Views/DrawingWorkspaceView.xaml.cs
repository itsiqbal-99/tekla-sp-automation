using SinglePartAutoFix.Application.Configuration;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Core.Infrastructure.Tekla;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SinglePartAutoFix.Wpf.Views
{
    public partial class DrawingWorkspaceView : UserControl
    {
        private readonly TeklaModelSession _teklaSession;
        private readonly string _expectedModelPath;
        private readonly Func<bool> _isAuthenticated;
        private readonly TeklaPartReader _partReader;
        private readonly DrawingCandidateBuilder _candidateBuilder;
        private readonly TeklaDrawingChecker _drawingChecker;
        private readonly DrawingProcessor _drawingProcessor;
        private readonly DrawingBatchRunner _batchRunner;
        private readonly TeklaNavigationService _navigationService;
        private readonly SimpleFileLogger _logger;
        private readonly IReadOnlyList<DrawingStandardProfile> _standardProfiles;
        private readonly TeklaDrawingStandardConfigurationValidator _standardValidator;
        private readonly Dictionary<string, DrawingStandardizationResult> _standardizationByPieceMark;
        private readonly Dictionary<string, DrawingVerificationResult> _verificationByPieceMark;

        private List<DrawingCandidate> _candidates;
        private List<DrawingProcessResult> _results;
        private bool _isBusy;
        private CancellationTokenSource _batchCancellation;

        public DrawingWorkspaceView(
            TeklaModelSession teklaSession,
            string displayName,
            string modelPath,
            Func<bool> isAuthenticated)
        {
            InitializeComponent();

            _teklaSession = teklaSession ?? throw new ArgumentNullException(nameof(teklaSession));
            _expectedModelPath = modelPath ?? string.Empty;
            _isAuthenticated = isAuthenticated ?? throw new ArgumentNullException(nameof(isAuthenticated));
            _partReader = new TeklaPartReader(_teklaSession);
            _candidateBuilder = new DrawingCandidateBuilder();
            _drawingChecker = new TeklaDrawingChecker(_teklaSession);
            _candidates = new List<DrawingCandidate>();
            _results = new List<DrawingProcessResult>();
            _standardizationByPieceMark = new Dictionary<string, DrawingStandardizationResult>(
                StringComparer.OrdinalIgnoreCase);
            _verificationByPieceMark = new Dictionary<string, DrawingVerificationResult>(
                StringComparer.OrdinalIgnoreCase);

            _standardProfiles = DrawingStandardConfiguration.CreateProfile();

            var configurationResolver = new TeklaDrawingStandardConfigurationResolver(_teklaSession);
            _standardValidator = new TeklaDrawingStandardConfigurationValidator(
                _teklaSession,
                configurationResolver);

            var activeProfile = _standardProfiles
                .FirstOrDefault(profile => profile.IsEnabled);

            _drawingProcessor = new DrawingProcessor(
                _drawingChecker,
                new TeklaDrawingCreator(_teklaSession),
                activeProfile);
            _batchRunner = new DrawingBatchRunner(_drawingProcessor);
            _navigationService = new TeklaNavigationService(_teklaSession);
            _logger = new SimpleFileLogger();

            UserDisplayNameText.Text = string.IsNullOrWhiteSpace(displayName)
                ? "Signed-in user"
                : displayName;

            UpdateConnectionStatus(true);
            UpdateStandardStatus();
            UpdatePreflightStatus();
            UpdateActionButtons();
        }

        public event EventHandler LogoutClicked;
        public event EventHandler TeklaConnectionLost;

        private async void SelectParts_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentModelAvailable())
            {
                return;
            }

            SetSelectionBusy(true);
            ClearResults();

            try
            {
                var selection = await Task.Run(ReadSelectedParts);
                _candidates = selection.Candidates;

                if (selection.Parts.Count == 0)
                {
                    SelectionTitleText.Text = "No parts selected";
                    SelectionDetailText.Text = "Select parts in Tekla Structures, then try again.";
                    return;
                }

                SelectionTitleText.Text = $"{_candidates.Count} drawing candidate(s)";
                SelectionDetailText.Text = $"Selected physical parts: {selection.Parts.Count}";
            }
            catch (Exception ex)
            {
                if (IsCurrentModelAvailable())
                {
                    SelectionTitleText.Text = "Unable to read selected parts";
                    SelectionDetailText.Text = ReportUnexpectedError(
                        ex,
                        "Part selection could not be completed.",
                        "SELECT PARTS");
                }
            }
            finally
            {
                SetSelectionBusy(false);
            }
        }

        private PartSelectionResult ReadSelectedParts()
        {
            var parts = _partReader.GetParts(new PartQuery
            {
                SelectionMode = PartSelectionMode.Selected
            });

            return new PartSelectionResult(parts, _candidateBuilder.Build(parts));
        }

        private async void RunDryCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentModelAvailable())
            {
                return;
            }

            if (_candidates.Count == 0)
            {
                ShowInformation("No drawing candidates are available.", "Dry Check");
                return;
            }

            SetDryCheckBusy(true);

            try
            {
                var results = await Task.Run(BuildDryCheckResults);
                ShowResults(results);
                _logger.Write("WPF DRY CHECK", results, _teklaSession.GetModelName());
            }
            catch (Exception ex)
            {
                if (IsCurrentModelAvailable())
                {
                    ShowError(
                        ReportUnexpectedError(ex, "The dry check could not be completed.", "DRY CHECK"),
                        "Dry Check Error");
                }
            }
            finally
            {
                SetDryCheckBusy(false);
            }
        }

        private List<DrawingProcessResult> BuildDryCheckResults()
        {
            _drawingChecker.Refresh();
            var results = _candidates
                .Select(candidate => _drawingProcessor.Process(candidate, dryRun: true))
                .ToList();

            foreach (var result in results)
            {
                if (result.Candidate == null || string.IsNullOrWhiteSpace(result.Candidate.PieceMark))
                {
                    continue;
                }

                DrawingStandardizationResult savedStandardization;
                if (_standardizationByPieceMark.TryGetValue(
                    result.Candidate.PieceMark,
                    out savedStandardization))
                {
                    result.Standardization = savedStandardization;
                }

                DrawingVerificationResult savedVerification;
                if (_verificationByPieceMark.TryGetValue(
                    result.Candidate.PieceMark,
                    out savedVerification))
                {
                    result.Verification = savedVerification;
                }
            }

            return results;
        }

        private void ShowResults(List<DrawingProcessResult> results)
        {
            _results = results ?? new List<DrawingProcessResult>();

            ReadyCountText.Text = CountResults(DrawingProcessStatus.ReadyToCreate).ToString();
            ExistingCountText.Text = CountResults(DrawingProcessStatus.Existing).ToString();
            ReviewCountText.Text = CountResults(DrawingProcessStatus.NeedReview).ToString();
            FailedCountText.Text = CountResults(DrawingProcessStatus.Failed).ToString();

            StatusFilterComboBox.SelectedIndex = 0;
            DrawingStateFilterComboBox.SelectedIndex = 0;
            GridSearchTextBox.Text = string.Empty;
            ApplyFilters();
            UpdateActionButtons();
        }

        private int CountResults(DrawingProcessStatus status)
        {
            return _results.Count(result => result.Status == status);
        }

        private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchText_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void DrawingStateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (CandidateDataGrid == null || StatusFilterComboBox == null || GridSearchTextBox == null)
            {
                return;
            }

            IEnumerable<DrawingProcessResult> visibleResults = _results;
            var selectedStatus = StatusFilterComboBox.SelectedItem as ComboBoxItem;
            string status = selectedStatus?.Content?.ToString() ?? "All Status";

            if (status != "All Status")
            {
                visibleResults = visibleResults.Where(result => result.Status.ToString() == status);
            }

            var selectedDrawingState = DrawingStateFilterComboBox?.SelectedItem as ComboBoxItem;
            string drawingState = selectedDrawingState?.Content?.ToString() ?? "All Drawings";
            if (drawingState == "Up to date")
            {
                visibleResults = visibleResults.Where(result =>
                    result.DrawingLookup?.Drawing?.UpToDateStatus == "DrawingIsUpToDate");
            }
            else if (drawingState == "Needs attention")
            {
                visibleResults = visibleResults.Where(NeedsDrawingAttention);
            }
            else if (drawingState == "Verified")
            {
                visibleResults = visibleResults.Where(result =>
                    result.Standardization?.Status == DrawingStandardizationStatus.Verified);
            }

            string searchText = GridSearchTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                visibleResults = visibleResults.Where(result => MatchesSearch(result, searchText));
            }

            CandidateDataGrid.ItemsSource = visibleResults.ToList();
        }

        private static bool MatchesSearch(DrawingProcessResult result, string searchText)
        {
            if (result == null)
            {
                return false;
            }

            var values = new[]
            {
                result.Candidate?.PieceMark,
                result.Candidate?.Profile,
                result.Candidate?.Material,
                result.Candidate?.MaterialType,
                result.Candidate?.PartCount.ToString(),
                result.Status.ToString(),
                result.StandardizationDisplay,
                result.DrawingStateDisplay,
                result.DrawingScaleDisplay,
                result.SuggestedAction,
                result.Message
            };

            return values.Any(value =>
                (value ?? string.Empty).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool NeedsDrawingAttention(DrawingProcessResult result)
        {
            if (result?.DrawingLookup?.Status == DrawingLookupStatus.Duplicate)
            {
                return true;
            }

            var drawing = result?.DrawingLookup?.Drawing;
            return drawing != null &&
                   (drawing.UpToDateStatus != "DrawingIsUpToDate" ||
                    drawing.IsLocked ||
                    drawing.IsFrozen ||
                    drawing.IsIssuedButModified);
        }

        private void CandidateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedCandidateDetails();
            UpdateActionButtons();
        }

        private void UpdateSelectedCandidateDetails()
        {
            var selected = CandidateDataGrid?.SelectedItem as DrawingProcessResult;
            if (selected == null)
            {
                SelectedDetailTitleText.Text = "Select a candidate to view details";
                SelectedDetailText.Text = "Drawing state, verification, and suggested actions appear here.";
                return;
            }

            SelectedDetailTitleText.Text =
                $"{selected.Candidate.PieceMarkDisplay} · {selected.Status} · {selected.DrawingStateDisplay}";

            string verification = selected.Verification == null
                ? string.Empty
                : $" Verification: {selected.Verification.Message}";
            SelectedDetailText.Text =
                $"{selected.DetailDisplay}{verification} Reference: {selected.OperationId ?? "-"}.";
        }

        private void FocusInModel_Click(object sender, RoutedEventArgs e)
        {
            var selected = CandidateDataGrid.SelectedItem as DrawingProcessResult;
            if (selected == null || !IsCurrentModelAvailable())
            {
                return;
            }

            try
            {
                string message;
                if (_navigationService.FocusInModel(selected.Candidate, out message))
                {
                    SelectedDetailText.Text = message;
                }
                else
                {
                    ShowInformation(message, "Focus in Model");
                }
            }
            catch (Exception ex)
            {
                ShowError(
                    ReportUnexpectedError(
                        ex,
                        "Tekla could not focus the selected model parts.",
                        "FOCUS IN MODEL"),
                    "Focus in Model");
            }
        }

        private void OpenDrawing_Click(object sender, RoutedEventArgs e)
        {
            var selected = CandidateDataGrid.SelectedItem as DrawingProcessResult;
            if (selected == null || !IsCurrentModelAvailable())
            {
                return;
            }

            try
            {
                string message;
                if (_navigationService.OpenDrawing(selected.Candidate, out message))
                {
                    SelectedDetailText.Text = message;
                }
                else
                {
                    ShowInformation(message, "Open Drawing");
                }
            }
            catch (Exception ex)
            {
                ShowError(
                    ReportUnexpectedError(
                        ex,
                        "Tekla could not open the selected drawing.",
                        "OPEN DRAWING"),
                    "Open Drawing");
            }
        }

        private async void RefreshDrawings_Click(object sender, RoutedEventArgs e)
        {
            if (_candidates.Count == 0 || !IsCurrentModelAvailable())
            {
                return;
            }

            SetDryCheckBusy(true);
            try
            {
                var results = await Task.Run(BuildDryCheckResults);
                ShowResults(results);
                _logger.Write("WPF REFRESH", results, _teklaSession.GetModelName());
            }
            catch (Exception ex)
            {
                ShowError(
                    ReportUnexpectedError(ex, "Drawing information could not be refreshed.", "REFRESH"),
                    "Refresh Error");
            }
            finally
            {
                SetDryCheckBusy(false);
            }
        }

        private void SelectReadyDrawings_Click(object sender, RoutedEventArgs e)
        {
            CandidateDataGrid.SelectedItems.Clear();

            var readyItems = CandidateDataGrid.Items
                .Cast<DrawingProcessResult>()
                .Where(result => result.Status == DrawingProcessStatus.ReadyToCreate)
                .Take(GetBatchSize())
                .ToList();

            foreach (var item in readyItems)
            {
                CandidateDataGrid.SelectedItems.Add(item);
            }

            if (readyItems.Count > 0)
            {
                CandidateDataGrid.ScrollIntoView(readyItems[0]);
            }
        }

        private async void CreateDrawings_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentModelAvailable())
            {
                return;
            }

            var standardValidation = ValidateDrawingStandard();
            if (!standardValidation.IsValid)
            {
                MessageBox.Show(
                    standardValidation.Message,
                    "Drawing Standard Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_results.Count == 0)
            {
                ShowInformation("Run the dry check before creating drawings.", "Create Drawings");
                return;
            }

            var selectedDrawings = CandidateDataGrid.SelectedItems
                .Cast<DrawingProcessResult>()
                .Where(result => result.Status == DrawingProcessStatus.ReadyToCreate)
                .ToList();

            if (selectedDrawings.Count == 0)
            {
                ShowInformation(
                    "Select at least one Ready to Create drawing from the grid.",
                    "No Drawing Selected");
                return;
            }

            int batchSize = GetBatchSize();
            if (selectedDrawings.Count > batchSize)
            {
                ShowInformation(
                    $"You selected {selectedDrawings.Count} drawing(s), but the batch size is {batchSize}. " +
                    "Reduce the selection or increase the batch size.",
                    "Batch Size Limit");
                return;
            }

            if (!ConfirmDrawingCreation(selectedDrawings))
            {
                return;
            }

            SetCreationBusy(true);
            _batchCancellation = new CancellationTokenSource();
            CancelBatchButton.Visibility = Visibility.Visible;

            try
            {
                var creationResults = await _batchRunner.RunAsync(
                    selectedDrawings.Select(item => item.Candidate).ToList(),
                    _batchCancellation.Token,
                    UpdateBatchProgress,
                    ValidateBatchContext);

                SaveStandardizationResults(creationResults);
                _logger.Write("WPF CONTROLLED BATCH", creationResults, _teklaSession.GetModelName());
                RefreshResults();
                ShowCreationSummary(creationResults);
            }
            catch (Exception ex)
            {
                if (IsCurrentModelAvailable())
                {
                    ShowError(
                        ReportUnexpectedError(ex, "The drawing batch could not be completed.", "CREATE BATCH"),
                        "Drawing Creation Error");
                }
            }
            finally
            {
                CancelBatchButton.Visibility = Visibility.Collapsed;
                _batchCancellation.Dispose();
                _batchCancellation = null;
                SetCreationBusy(false);
            }
        }

        private string ValidateBatchContext()
        {
            try
            {
                if (!_isAuthenticated())
                {
                    return "The authenticated session is no longer valid.";
                }

                if (!_teklaSession.IsConnected())
                {
                    return "Tekla Structures is no longer connected.";
                }

                string currentPath = _teklaSession.GetModelPath() ?? string.Empty;
                if (!string.Equals(_expectedModelPath, currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    return "The active Tekla model changed after the dry check.";
                }

                var standardValidation = ValidateDrawingStandard();
                return standardValidation.IsValid ? string.Empty : standardValidation.Message;
            }
            catch
            {
                return "The Tekla preflight check could not be completed.";
            }
        }

        private void UpdateBatchProgress(int completed, int total, string message)
        {
            SelectedDetailTitleText.Text = $"Batch progress — {completed}/{total}";
            SelectedDetailText.Text = message;
        }

        private void CancelBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_batchCancellation == null || _batchCancellation.IsCancellationRequested)
            {
                return;
            }

            _batchCancellation.Cancel();
            CancelBatchButton.Content = "Cancelling...";
            SelectedDetailText.Text = "The batch will stop after the current drawing operation finishes.";
        }

        private bool ConfirmDrawingCreation(IReadOnlyList<DrawingProcessResult> drawings)
        {
            string drawingList = string.Join(
                Environment.NewLine,
                drawings.Select(item =>
                    $"{item.Candidate.PieceMark} | {item.Candidate.Profile} | Qty: {item.Candidate.PartCount}"));

            var answer = MessageBox.Show(
                $"You are about to create {drawings.Count} drawing(s).\n\n" +
                $"{drawingList}\n\nContinue with drawing creation?",
                "Confirm Drawing Creation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return answer == MessageBoxResult.Yes;
        }

        private void SaveStandardizationResults(IEnumerable<DrawingProcessResult> creationResults)
        {
            foreach (var result in creationResults)
            {
                if (result.Candidate == null ||
                    string.IsNullOrWhiteSpace(result.Candidate.PieceMark) ||
                    result.Standardization == null)
                {
                    continue;
                }

                _standardizationByPieceMark[result.Candidate.PieceMark] = result.Standardization;
                if (result.Verification != null)
                {
                    _verificationByPieceMark[result.Candidate.PieceMark] = result.Verification;
                }
            }
        }

        private void RefreshResults()
        {
            if (_candidates.Count > 0)
            {
                ShowResults(BuildDryCheckResults());
            }
        }

        private static void ShowCreationSummary(IReadOnlyList<DrawingProcessResult> results)
        {
            int created = results.Count(result => result.Status == DrawingProcessStatus.Created);
            int existing = results.Count(result => result.Status == DrawingProcessStatus.Existing);
            int review = results.Count(result => result.Status == DrawingProcessStatus.NeedReview);
            int failed = results.Count(result => result.Status == DrawingProcessStatus.Failed);
            int cancelled = results.Count(result => result.Status == DrawingProcessStatus.Cancelled);

            string details = string.Join(
                Environment.NewLine,
                results.Select(result =>
                    $"{result.Candidate?.PieceMarkDisplay ?? "Unknown"} | {result.Status} | {result.OperationId}"));

            var icon = failed > 0 || review > 0 || cancelled > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;

            MessageBox.Show(
                $"Processed   : {results.Count}\n" +
                $"Created     : {created}\n" +
                $"Existing    : {existing}\n" +
                $"Need Review : {review}\n" +
                $"Failed      : {failed}\n\n" +
                $"Cancelled   : {cancelled}\n\n" +
                $"Details:\n{details}",
                "Drawing Creation Completed",
                MessageBoxButton.OK,
                icon);
        }

        private int GetBatchSize()
        {
            var selectedItem = BatchSizeComboBox.SelectedItem as ComboBoxItem;
            int batchSize;

            return selectedItem != null &&
                   int.TryParse(selectedItem.Content.ToString(), out batchSize)
                ? batchSize
                : 3;
        }

        private bool IsCurrentModelAvailable()
        {
            try
            {
                bool connected = _teklaSession.IsConnected();
                string currentModelName = connected ? _teklaSession.GetModelName() : string.Empty;
                string currentModelPath = connected ? _teklaSession.GetModelPath() ?? string.Empty : string.Empty;

                if (connected &&
                    !string.IsNullOrWhiteSpace(currentModelName) &&
                    string.Equals(_expectedModelPath, currentModelPath, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateConnectionStatus(true);
                    return true;
                }
            }
            catch
            {
                // The connection screen owns the user-facing recovery message.
            }

            ResetWorkspace();
            UpdateConnectionStatus(false);
            TeklaConnectionLost?.Invoke(this, EventArgs.Empty);
            return false;
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            if (isConnected)
            {
                string modelName = _teklaSession.GetModelName();
                TeklaStatusText.Text = string.IsNullOrWhiteSpace(modelName)
                    ? "Tekla connected"
                    : $"Connected — {modelName}";
                TeklaStatusText.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                TeklaStatusIndicator.Fill = (Brush)FindResource("PrimaryBrush");
                UpdatePreflightStatus();
                return;
            }

            TeklaStatusText.Text = "Tekla disconnected";
            TeklaStatusText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            UpdatePreflightStatus();
        }

        private void UpdateStandardStatus()
        {
            var validation = ValidateDrawingStandard();
            var activeProfiles = _standardProfiles
                .Where(profile => profile.IsEnabled)
                .ToList();
            string profileDetail = activeProfiles.Count == 1
                ? $"Profile: {activeProfiles[0].Name} v{activeProfiles[0].Version}\n" +
                  $"Attribute: {activeProfiles[0].DrawingAttributeName}\n" +
                  $"Required file: {activeProfiles[0].RequiredAttributeFileName}\n"
                : string.Empty;
            string pathDetail = string.IsNullOrWhiteSpace(validation.ResolvedPath)
                ? string.Empty
                : $"\nResolved path: {validation.ResolvedPath}";
            StandardizationStatusText.ToolTip =
                $"{profileDetail}Validation: {validation.Status} — {validation.Message}{pathDetail}";

            switch (validation.Status)
            {
                case DrawingStandardValidationStatus.Ready:
                    StandardizationStatusText.Text = "Ready · Test Profile";
                    StandardizationStatusText.Foreground = (Brush)FindResource("PrimaryBrush");
                    break;

                case DrawingStandardValidationStatus.TeklaNotConnected:
                    StandardizationStatusText.Text = "Tekla Not Connected";
                    StandardizationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                    break;

                case DrawingStandardValidationStatus.ConfigurationMissing:
                    StandardizationStatusText.Text = "Configuration Missing";
                    StandardizationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                    break;

                default:
                    StandardizationStatusText.Text = "Not Configured";
                    StandardizationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                    break;
            }

            UpdatePreflightStatus();
        }

        private void UpdatePreflightStatus()
        {
            if (PreflightStatusText == null)
            {
                return;
            }

            bool authenticated = false;
            bool teklaConnected = false;
            bool activeModel = false;
            bool drawingApi = false;
            bool standardReady = false;

            try
            {
                authenticated = _isAuthenticated != null && _isAuthenticated();
                teklaConnected = _teklaSession != null && _teklaSession.IsConnected();
                activeModel = teklaConnected &&
                              !string.IsNullOrWhiteSpace(_teklaSession.GetModelName()) &&
                              string.Equals(
                                  _expectedModelPath,
                                  _teklaSession.GetModelPath() ?? string.Empty,
                                  StringComparison.OrdinalIgnoreCase);
                drawingApi = teklaConnected && _navigationService.IsDrawingApiConnected();
                standardReady = ValidateDrawingStandard().IsValid;
            }
            catch
            {
                // Preflight is informational; the workflow gates provide the actionable error.
            }

            PreflightStatusText.Text =
                $"Preflight: Authentication {ReadyLabel(authenticated)}  ·  " +
                $"Tekla {ReadyLabel(teklaConnected)}  ·  " +
                $"Active Model {ReadyLabel(activeModel)}  ·  " +
                $"Drawing API {ReadyLabel(drawingApi)}  ·  " +
                $"Drawing Standard {ReadyLabel(standardReady)}";

            PreflightStatusText.Foreground = authenticated && teklaConnected && activeModel && drawingApi && standardReady
                ? (Brush)FindResource("PrimaryBrush")
                : new SolidColorBrush(Color.FromRgb(180, 83, 9));
        }

        private static string ReadyLabel(bool isReady)
        {
            return isReady ? "Ready" : "Check";
        }

        private DrawingStandardValidationResult ValidateDrawingStandard()
        {
            var activeProfiles = _standardProfiles
                .Where(profile => profile.IsEnabled)
                .ToList();

            if (activeProfiles.Count == 0)
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.NotConfigured,
                    "No enabled drawing standard profile is configured.");
            }

            if (activeProfiles.Count > 1)
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.ConfigurationMissing,
                    "More than one drawing standard profile is enabled. Enable exactly one profile.");
            }

            return _standardValidator.Validate(activeProfiles[0]);
        }

        private void ClearResults()
        {
            _results.Clear();
            _standardizationByPieceMark.Clear();
            _verificationByPieceMark.Clear();
            CandidateDataGrid.ItemsSource = null;
            StatusFilterComboBox.SelectedIndex = 0;
            DrawingStateFilterComboBox.SelectedIndex = 0;
            GridSearchTextBox.Text = string.Empty;
            SelectedDetailTitleText.Text = "Select a candidate to view details";
            SelectedDetailText.Text = "Drawing state, verification, and suggested actions appear here.";
            SetCountText("-");
            UpdateActionButtons();
        }

        private void ResetWorkspace()
        {
            _candidates.Clear();
            ClearResults();
            SelectionTitleText.Text = "Connection lost";
            SelectionDetailText.Text = "Reconnect to Tekla Structures to continue.";
        }

        private void SetCountText(string value)
        {
            ReadyCountText.Text = value;
            ExistingCountText.Text = value;
            ReviewCountText.Text = value;
            FailedCountText.Text = value;
        }

        private void SetSelectionBusy(bool isBusy)
        {
            _isBusy = isBusy;
            SelectPartsButton.Content = isBusy ? "Reading Parts..." : "Select Parts";
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
            UpdateActionButtons();
        }

        private void SetDryCheckBusy(bool isBusy)
        {
            _isBusy = isBusy;
            DryRunButton.Content = isBusy ? "Checking..." : "Run Dry Check";
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
            UpdateActionButtons();
        }

        private void SetCreationBusy(bool isBusy)
        {
            _isBusy = isBusy;
            CreateDrawingsButton.Content = isBusy ? "Creating..." : "Create Drawings";
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
            if (!isBusy)
            {
                CancelBatchButton.Content = "Cancel Batch";
            }
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool hasCandidates = _candidates.Count > 0;
            bool hasReadyDrawings = _results.Any(
                result => result.Status == DrawingProcessStatus.ReadyToCreate);
            var selected = CandidateDataGrid?.SelectedItem as DrawingProcessResult;

            SelectPartsButton.IsEnabled = !_isBusy;
            DryRunButton.IsEnabled = !_isBusy && hasCandidates;
            StatusFilterComboBox.IsEnabled = !_isBusy;
            DrawingStateFilterComboBox.IsEnabled = !_isBusy;
            GridSearchTextBox.IsEnabled = !_isBusy;
            BatchSizeComboBox.IsEnabled = !_isBusy;
            SelectBatchButton.IsEnabled = !_isBusy && hasReadyDrawings;
            CreateDrawingsButton.IsEnabled = !_isBusy && hasReadyDrawings;
            RefreshDrawingsButton.IsEnabled = !_isBusy && hasCandidates;
            FocusInModelButton.IsEnabled = !_isBusy && selected?.Candidate != null;
            OpenDrawingButton.IsEnabled = !_isBusy &&
                                          selected?.DrawingLookup?.Status == DrawingLookupStatus.Found;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            ResetWorkspace();
            LogoutClicked?.Invoke(this, EventArgs.Empty);
        }

        private static void ShowInformation(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string ReportUnexpectedError(
            Exception exception,
            string userMessage,
            string processName)
        {
            string operationId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            string modelName;
            try
            {
                modelName = _teklaSession.GetModelName();
            }
            catch
            {
                modelName = "Unknown";
            }

            _logger.WriteError(processName, modelName, operationId, exception);
            return $"{userMessage} Reference: {operationId}.";
        }

        private sealed class PartSelectionResult
        {
            public PartSelectionResult(IReadOnlyList<PartInfo> parts, List<DrawingCandidate> candidates)
            {
                Parts = parts;
                Candidates = candidates;
            }

            public IReadOnlyList<PartInfo> Parts { get; }
            public List<DrawingCandidate> Candidates { get; }
        }
    }
}
