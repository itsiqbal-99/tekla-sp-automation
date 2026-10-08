using SinglePartAutoFix.Application.Configuration;
using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Core.Infrastructure.Tekla;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.src.Application.Interfaces;
using SinglePartAutoFix.src.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly TeklaPartReader _partReader;
        private readonly DrawingCandidateBuilder _candidateBuilder;
        private readonly DrawingProcessor _drawingProcessor;
        private readonly IDrawingStandardProfileProvider _standardProfileProvider;
        private readonly IDrawingStandardConfigurationValidator _standardValidator;
        private readonly Dictionary<string, DrawingStandardizationResult> _standardizationByPieceMark;

        private List<DrawingCandidate> _candidates;
        private List<DrawingProcessResult> _results;
        private bool _isBusy;

        public DrawingWorkspaceView(TeklaModelSession teklaSession, string displayName, string modelPath)
        {
            InitializeComponent();

            _teklaSession = teklaSession ?? throw new ArgumentNullException(nameof(teklaSession));
            _expectedModelPath = modelPath ?? string.Empty;
            _partReader = new TeklaPartReader(_teklaSession);
            _candidateBuilder = new DrawingCandidateBuilder();
            _candidates = new List<DrawingCandidate>();
            _results = new List<DrawingProcessResult>();
            _standardizationByPieceMark = new Dictionary<string, DrawingStandardizationResult>(
                StringComparer.OrdinalIgnoreCase);

            var profiles = DrawingStandardConfiguration.CreateProfile();
            _standardProfileProvider = new DrawingStandardProfileProvider(profiles);

            var configurationResolver = new TeklaDrawingStandardConfigurationResolver(_teklaSession);
            _standardValidator = new TeklaDrawingStandardConfigurationValidator(
                _teklaSession,
                configurationResolver);

            var activeProfile = _standardProfileProvider.GetProfiles()
                .FirstOrDefault(profile => profile.IsEnabled);

            _drawingProcessor = new DrawingProcessor(
                new TeklaDrawingChecker(_teklaSession),
                new TeklaDrawingCreator(_teklaSession),
                new NoOpDrawingStandardizer(),
                activeProfile);

            UserDisplayNameText.Text = string.IsNullOrWhiteSpace(displayName)
                ? "Signed-in user"
                : displayName;

            UpdateConnectionStatus(true);
            UpdateStandardStatus();
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
                    SelectionDetailText.Text = ex.Message;
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
            }
            catch (Exception ex)
            {
                if (IsCurrentModelAvailable())
                {
                    ShowError(ex.Message, "Dry Check Error");
                }
            }
            finally
            {
                SetDryCheckBusy(false);
            }
        }

        private List<DrawingProcessResult> BuildDryCheckResults()
        {
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
                result.Message
            };

            return values.Any(value =>
                (value ?? string.Empty).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
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

        private void CreateDrawings_Click(object sender, RoutedEventArgs e)
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

            try
            {
                var creationResults = selectedDrawings
                    .Select(item => _drawingProcessor.Process(item.Candidate, dryRun: false))
                    .ToList();

                SaveStandardizationResults(creationResults);
                RefreshResults();
                ShowCreationSummary(creationResults);
            }
            catch (Exception ex)
            {
                if (IsCurrentModelAvailable())
                {
                    ShowError(ex.Message, "Drawing Creation Error");
                }
            }
            finally
            {
                SetCreationBusy(false);
            }
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

            string details = string.Join(
                Environment.NewLine,
                results.Select(result => $"{result.Candidate.PieceMark} | {result.Status}"));

            var icon = failed > 0 || review > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;

            MessageBox.Show(
                $"Processed   : {results.Count}\n" +
                $"Created     : {created}\n" +
                $"Existing    : {existing}\n" +
                $"Need Review : {review}\n" +
                $"Failed      : {failed}\n\n" +
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
                return;
            }

            TeklaStatusText.Text = "Tekla disconnected";
            TeklaStatusText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }

        private void UpdateStandardStatus()
        {
            var validation = ValidateDrawingStandard();
            StandardizationStatusText.ToolTip = validation.Message;

            switch (validation.Status)
            {
                case DrawingStandardValidationStatus.Ready:
                    StandardizationStatusText.Text = "Ready";
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
        }

        private DrawingStandardValidationResult ValidateDrawingStandard()
        {
            var activeProfile = _standardProfileProvider.GetProfiles()
                .FirstOrDefault(profile => profile.IsEnabled);

            return activeProfile == null
                ? DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.NotConfigured,
                    "No enabled drawing standard profile is configured.")
                : _standardValidator.Validate(activeProfile);
        }

        private void ClearResults()
        {
            _results.Clear();
            _standardizationByPieceMark.Clear();
            CandidateDataGrid.ItemsSource = null;
            StatusFilterComboBox.SelectedIndex = 0;
            GridSearchTextBox.Text = string.Empty;
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
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool hasCandidates = _candidates.Count > 0;
            bool hasReadyDrawings = _results.Any(
                result => result.Status == DrawingProcessStatus.ReadyToCreate);

            SelectPartsButton.IsEnabled = !_isBusy;
            DryRunButton.IsEnabled = !_isBusy && hasCandidates;
            StatusFilterComboBox.IsEnabled = !_isBusy;
            GridSearchTextBox.IsEnabled = !_isBusy;
            BatchSizeComboBox.IsEnabled = !_isBusy;
            SelectBatchButton.IsEnabled = !_isBusy && hasReadyDrawings;
            CreateDrawingsButton.IsEnabled = !_isBusy && hasReadyDrawings;
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
