using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.src.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SinglePartAutoFix.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TeklaModelSession _teklaSession;
        private readonly TeklaPartReader _partReader;
        private readonly DrawingCandidateBuilder _candidateBuilder;
        private List<DrawingCandidate> _drawingCandidates;
        private readonly DrawingProcessor _drawingProcessor;
        private List<DrawingProcessResult> _dryRunResults;

        private readonly Dictionary<string, DrawingStandardizationResult> _standardizationResults = new Dictionary<string, DrawingStandardizationResult>(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            _teklaSession = new TeklaModelSession();
            _partReader = new TeklaPartReader(_teklaSession);
            _candidateBuilder = new DrawingCandidateBuilder();

            var drawingChecker = new TeklaDrawingChecker(_teklaSession);
            var drawingCreator = new TeklaDrawingCreator(_teklaSession);
            var drawingStandardizer = new NoOpDrawingStandardizer();

            _drawingProcessor = new DrawingProcessor(
                drawingChecker,
                drawingCreator,
                drawingStandardizer);

            _drawingCandidates = new List<DrawingCandidate>();
            _dryRunResults = new List<DrawingProcessResult>();
            //ShowTeklaRuntimeInfo();
            UpdateTeklaConnectionStatus();

        }

        private void UpdateTeklaConnectionStatus()
        {
            try
            {
                bool isConnected = _teklaSession.IsConnected();
                if (isConnected)
                {
                    TeklaStatusText.Text = "Tekla Connected";
                    TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else
                {
                    TeklaStatusText.Text = "Tekla Not Connected";
                    TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(156, 163, 175));

                    MessageBox.Show(
                        "TeklaModelSession was created successfully, but GetConnectionStatus() returned false", "Tekla Connection Diagnostic", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                TeklaStatusText.Text = $"Error: {ex.Message}";
                TeklaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(156, 163, 175));

                MessageBox.Show(
                       ex.ToString(), "Tekla Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        private void SelectPartsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_teklaSession.IsConnected())
                {
                    SelectionTitleText.Text = "Tekla is Not Connected";
                    SelectionDetailText.Text = "Open Tekla Structures and load a model first.";
                    UpdateTeklaConnectionStatus();
                    return;
                }

                _dryRunResults.Clear();
                _standardizationResults.Clear();

                ReadyCountText.Text = "-";
                ExistingCountText.Text = "-";
                ReviewCountText.Text = "-";
                FailedCountText.Text = "-";
                CreateDrawingsButton.IsEnabled = false;
                SelectBatchButton.IsEnabled = false;

                StatusFilterComboBox.SelectedIndex = 0;
                CandidateDataGrid.ItemsSource = null;

                var query = new PartQuery
                {
                    SelectionMode = PartSelectionMode.Selected
                };

                var parts = _partReader.GetParts(query);
                _drawingCandidates = _candidateBuilder.Build(parts);

                if (parts.Count == 0)
                {
                    SelectionTitleText.Text = "No Parts Selected";
                    SelectionDetailText.Text = "Please select parts in Tekla Structures and try again.";
                    DryRunButton.IsEnabled = false;
                    return;
                }

                SelectionTitleText.Text = $"{_drawingCandidates.Count} drawing candidate(s)";

                SelectionDetailText.Text = $"Selected physical parts: {parts.Count}";

                DryRunButton.IsEnabled = _drawingCandidates.Count > 0;

            }
            catch (System.Exception ex)
            {
                SelectionTitleText.Text = "Unable to read selected parts";
                SelectionDetailText.Text = ex.Message;
            }
        }

        private void DryRunButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_drawingCandidates == null ||
                    _drawingCandidates.Count == 0)
                {
                    MessageBox.Show(
                        "No drawing candidates available.",
                        "Dry Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                RefreshDryRunResults();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Dry Check Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_dryRunResults == null ||
                _dryRunResults.Count == 0 ||
                StatusFilterComboBox.SelectedItem == null)
            {
                return;
            }

            var selectedItem =
                StatusFilterComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;

            if (selectedItem == null)
            {
                return;
            }

            string selectedStatus = selectedItem.Content.ToString();

            if (selectedStatus == "All Status")
            {
                CandidateDataGrid.ItemsSource = _dryRunResults;
                return;
            }

            CandidateDataGrid.ItemsSource = _dryRunResults
                .Where(x => x.Status.ToString() == selectedStatus)
                .ToList();
        }

        private void CreateDrawingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int maxItems = GetSelectedBatchSize();

                if (_dryRunResults == null || _dryRunResults.Count == 0)
                {
                    MessageBox.Show(
                        "Please run the dry check before creating drawings.",
                        "Create Drawings",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var selectedItems = CandidateDataGrid.SelectedItems
                    .Cast<DrawingProcessResult>()
                    .ToList();

                var selectedReadyItems = selectedItems
                    .Where(x =>
                        x.Status == DrawingProcessStatus.ReadyToCreate)
                    .ToList();

                if (selectedReadyItems.Count > maxItems)
                {
                    MessageBox.Show(
                        $"You selected {selectedReadyItems.Count} drawing(s), " +
                        $"but the current batch size is {maxItems}.\n\n" +
                        "Please reduce the selection or increase the batch size.",
                        "Batch Size Limit",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                if (selectedReadyItems.Count == 0)
                {
                    MessageBox.Show(
                        "Please select at least one Ready to Create drawing from the grid.",
                        "No Drawing Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var readyItems = selectedReadyItems;

                if (readyItems.Count == 0)
                {
                    MessageBox.Show(
                        "No drawings are ready to create.",
                        "Create Drawings",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                string drawingList = string.Join(
                    "\n",
                    readyItems.Select(x =>
                        $"{x.Candidate.PieceMark} | " +
                        $"{x.Candidate.Profile} | " +
                        $"Qty: {x.Candidate.PartCount}"));

                var confirmation = MessageBox.Show(
                    $"You are about to create {readyItems.Count} drawing(s).\n\n" +
                    $"{drawingList}\n\n" +
                    "Please review the list before continuing.\n\n" +
                    "Continue with drawing creation?",
                    "Confirm Drawing Creation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes)
                {
                    return;
                }

                SetProcessingState(true);

                List<DrawingProcessResult> creationResults;

                try
                {
                    creationResults = readyItems.Select(x =>
                            _drawingProcessor.Process(
                                x.Candidate,
                                dryRun: false))
                        .ToList();

                    foreach (var result in creationResults)
                    {
                        if (result.Candidate == null || string.IsNullOrWhiteSpace(result.Candidate.PieceMark) || result.Standardization == null)
                        {
                            continue;
                        }

                        _standardizationResults[result.Candidate.PieceMark] = result.Standardization;
                    }

                    RefreshDryRunResults();
                }
                finally
                {
                    SetProcessingState(false);
                }

                int created = creationResults.Count(
                    x => x.Status == DrawingProcessStatus.Created);

                int existing = creationResults.Count(
                    x => x.Status == DrawingProcessStatus.Existing);

                int failed = creationResults.Count(
                    x => x.Status == DrawingProcessStatus.Failed);

                int needReview = creationResults.Count(
                    x => x.Status == DrawingProcessStatus.NeedReview);


                string resultDetails = string.Join("\n",
                creationResults.Select(x =>
                    $"{x.Candidate.PieceMark} | {x.Status}"));

                MessageBoxImage resultIcon;

                if (failed > 0 || needReview > 0)
                {
                    resultIcon = MessageBoxImage.Warning;
                }
                else
                {
                    resultIcon = MessageBoxImage.Information;
                }



                MessageBox.Show(
                $"Processed   : {creationResults.Count}\n" +
                $"Created     : {created}\n" +
                $"Existing    : {existing}\n" +
                $"Need Review : {needReview}\n" +
                $"Failed      : {failed}\n\n" +
                $"Details:\n{resultDetails}",
                "Drawing Creation Completed",
                MessageBoxButton.OK,
                resultIcon);

            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Drawing Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshDryRunResults()
        {
            if (_drawingCandidates == null ||
                _drawingCandidates.Count == 0)
            {
                return;
            }

            _dryRunResults = _drawingCandidates
                .Select(candidate =>
                    _drawingProcessor.Process(
                        candidate,
                        dryRun: true))
                .ToList();

            foreach (var result in _dryRunResults)
            {
                if (result.Candidate == null || string.IsNullOrWhiteSpace(result.Candidate.PieceMark))
                {
                    continue;
                }

                DrawingStandardizationResult standardizationResult;

                if (_standardizationResults.TryGetValue(
                    result.Candidate.PieceMark,
                    out standardizationResult))
                {
                    result.Standardization = standardizationResult;
                }
            }

            int ready = _dryRunResults.Count(
                x => x.Status == DrawingProcessStatus.ReadyToCreate);

            int existing = _dryRunResults.Count(
                x => x.Status == DrawingProcessStatus.Existing);

            int needReview = _dryRunResults.Count(
                x => x.Status == DrawingProcessStatus.NeedReview);

            int failed = _dryRunResults.Count(
                x => x.Status == DrawingProcessStatus.Failed);

            ReadyCountText.Text = ready.ToString();
            ExistingCountText.Text = existing.ToString();
            ReviewCountText.Text = needReview.ToString();
            FailedCountText.Text = failed.ToString();

            StatusFilterComboBox.SelectedIndex = 0;

            CandidateDataGrid.ItemsSource = null;
            CandidateDataGrid.ItemsSource = _dryRunResults;

            CreateDrawingsButton.IsEnabled = ready > 0;
            SelectBatchButton.IsEnabled = ready > 0;
        }

        private int GetSelectedBatchSize()
        {
            var selectedItem =
                BatchSizeComboBox.SelectedItem as ComboBoxItem;

            if (selectedItem == null)
            {
                return 3;
            }

            int batchSize;

            if (!int.TryParse(
                selectedItem.Content.ToString(),
                out batchSize))
            {
                return 3;
            }

            return batchSize;
        }

        private void SetProcessingState(bool isProcessing)
        {
            SelectPartsButton.IsEnabled = !isProcessing;

            DryRunButton.IsEnabled = !isProcessing && _drawingCandidates != null && _drawingCandidates.Count > 0;

            BatchSizeComboBox.IsEnabled = !isProcessing;
            SelectBatchButton.IsEnabled = !isProcessing &&
                _dryRunResults != null &&
                _dryRunResults.Any(x => x.Status == DrawingProcessStatus.ReadyToCreate);

            if (isProcessing)
            {
                CreateDrawingsButton.IsEnabled = false;
                CreateDrawingsButton.Content = "Processing...";
                Mouse.OverrideCursor = Cursors.Wait;
            }
            else
            {
                CreateDrawingsButton.Content = "Create Drawings";
                int readyCount = _dryRunResults == null ? 0 : _dryRunResults.Count(x => x.Status == DrawingProcessStatus.ReadyToCreate);

                CreateDrawingsButton.IsEnabled = readyCount > 0;
                Mouse.OverrideCursor = null;

                ;

            }
        }

        private void SelectBatchButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_dryRunResults == null ||
                _dryRunResults.Count == 0)
            {
                return;
            }

            int batchSize = GetSelectedBatchSize();

            CandidateDataGrid.SelectedItems.Clear();

            // Use the rows currently visible in the grid so the selection
            // respects the active status filter.
            var visibleReadyItems = CandidateDataGrid.Items
                .Cast<DrawingProcessResult>()
                .Where(x =>
                    x.Status == DrawingProcessStatus.ReadyToCreate)
                .Take(batchSize)
                .ToList();

            foreach (var item in visibleReadyItems)
            {
                CandidateDataGrid.SelectedItems.Add(item);
            }

            if (visibleReadyItems.Count > 0)
            {
                CandidateDataGrid.ScrollIntoView(
                    visibleReadyItems.First());
            }
        }

        private void ShowTeklaRuntimeInfo()
        {
            var teklaAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Tekla.Structures.Model");

            string assemblyInfo = teklaAssembly == null
            ? "Tekla.Structures.Model is not loaded"
            : $"{teklaAssembly.FullName}\n\nLoaded From:\n{teklaAssembly.Location}";

            MessageBox.Show(
                $"Process 64-bit: {Environment.Is64BitProcess}\n\n" +
                $"Tekla Assembly:\n{assemblyInfo}",
                "Tekla Runtime Diagnostic",
                MessageBoxButton.OK,
                MessageBoxImage.Information);


        }
    }
}