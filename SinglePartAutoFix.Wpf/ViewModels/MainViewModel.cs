using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Wpf.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace SinglePartAutoFix.Wpf.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly SinglePartAutomationService _service;
        private IReadOnlyList<DrawingProcessResult> _dryRunResults = new List<DrawingProcessResult>();
        private bool _isBusy;
        private bool _isConnected;
        private bool _hasPreparedCandidates;
        private bool _dryRunComplete;
        private string _modelName;
        private string _selectedStatus = "All";
        private string _searchText = string.Empty;
        private string _batchLimit = "3";
        private int _progressValue;
        private int _progressMaximum = 1;
        private string _currentPieceMark = "—";
        private string _resultSummary = "No batch has been run.";
        private string _technicalDetails;

        public MainViewModel(SinglePartAutomationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Candidates = new ObservableCollection<CandidateRowViewModel>();
            Activity = new ObservableCollection<string>();
            StatusOptions = new[] { "All", "Ready", "Existing", "Need Review", "Failed" };
            CandidatesView = CollectionViewSource.GetDefaultView(Candidates);
            CandidatesView.Filter = FilterCandidate;
            LoadCommand = new RelayCommand(LoadSelectedParts, () => !IsBusy);
            DryRunCommand = new AsyncRelayCommand(RunDryRunAsync, () => !IsBusy && HasPreparedCandidates);
            GenerateCommand = new AsyncRelayCommand(GenerateAsync, CanGenerate);
            RefreshConnection();
        }

        public ObservableCollection<CandidateRowViewModel> Candidates { get; }
        public ObservableCollection<string> Activity { get; }
        public ICollectionView CandidatesView { get; }
        public IReadOnlyList<string> StatusOptions { get; }
        public RelayCommand LoadCommand { get; }
        public AsyncRelayCommand DryRunCommand { get; }
        public AsyncRelayCommand GenerateCommand { get; }
        public Func<BatchPlan, bool> ConfirmBatch { get; set; }
        public Action<string, string> ShowMessage { get; set; }

        public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) RaiseCommandStates(); } }
        public bool IsConnected { get => _isConnected; private set { if (Set(ref _isConnected, value)) OnPropertyChanged(nameof(ConnectionText)); } }
        public string ConnectionText => IsConnected ? "Connected" : "Disconnected";
        public string ModelName { get => _modelName; private set => Set(ref _modelName, value); }
        public bool HasPreparedCandidates { get => _hasPreparedCandidates; private set { if (Set(ref _hasPreparedCandidates, value)) RaiseCommandStates(); } }
        public bool DryRunComplete { get => _dryRunComplete; private set { if (Set(ref _dryRunComplete, value)) RaiseCommandStates(); } }
        public int SelectedPartCount { get; private set; }
        public int CandidateCount => Candidates.Count;
        public int ReadyCount => Candidates.Count(row => row.Status == DrawingProcessStatus.ReadyToCreate);
        public int ExistingCount => Candidates.Count(row => row.Status == DrawingProcessStatus.Existing);
        public int NeedReviewCount => Candidates.Count(row => row.Status == DrawingProcessStatus.NeedReview);
        public int FailedCount => Candidates.Count(row => row.Status == DrawingProcessStatus.Failed);
        public int CheckedReadyCount => Candidates.Count(row => row.IsSelected && row.IsEligible);
        public string ProgressText => IsBusy ? $"Processing {ProgressValue} of {ProgressMaximum}" : "Idle";
        public int ProgressValue { get => _progressValue; private set { if (Set(ref _progressValue, value)) OnPropertyChanged(nameof(ProgressText)); } }
        public int ProgressMaximum { get => _progressMaximum; private set { if (Set(ref _progressMaximum, value)) OnPropertyChanged(nameof(ProgressText)); } }
        public string CurrentPieceMark { get => _currentPieceMark; private set => Set(ref _currentPieceMark, value); }
        public string ResultSummary { get => _resultSummary; private set => Set(ref _resultSummary, value); }
        public string TechnicalDetails { get => _technicalDetails; private set { if (Set(ref _technicalDetails, value)) OnPropertyChanged(nameof(HasTechnicalDetails)); } }
        public bool HasTechnicalDetails => !string.IsNullOrWhiteSpace(TechnicalDetails);

        public string SelectedStatus
        {
            get => _selectedStatus;
            set { if (Set(ref _selectedStatus, value)) CandidatesView.Refresh(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { if (Set(ref _searchText, value)) CandidatesView.Refresh(); }
        }

        public string BatchLimit
        {
            get => _batchLimit;
            set { if (Set(ref _batchLimit, value)) RaiseCommandStates(); }
        }

        private void RefreshConnection()
        {
            try
            {
                var connection = _service.GetConnectionInfo();
                IsConnected = connection.IsConnected;
                ModelName = connection.IsConnected ? connection.ModelName : "No active Tekla model";
                AddActivity(connection.IsConnected ? $"Connected to Tekla model: {connection.ModelName}" : "No Tekla model connection detected.");
            }
            catch (Exception ex) { HandleError("Could not check the Tekla connection.", ex); }
        }

        private void LoadSelectedParts()
        {
            IsBusy = true;
            try
            {
                RefreshConnection();
                if (!IsConnected) { ShowMessage?.Invoke("Tekla Connection", "Open a Tekla model before loading selected parts."); return; }
                var preparation = _service.PrepareCandidates(new PartQuery { SelectionMode = PartSelectionMode.Selected });
                Candidates.Clear();
                foreach (var candidate in preparation.Candidates)
                {
                    var row = new CandidateRowViewModel(candidate);
                    row.PropertyChanged += CandidatePropertyChanged;
                    Candidates.Add(row);
                }
                SelectedPartCount = preparation.SelectedPartCount;
                HasPreparedCandidates = Candidates.Count > 0;
                DryRunComplete = false;
                _dryRunResults = new List<DrawingProcessResult>();
                ResultSummary = "No batch has been run.";
                AddActivity($"{SelectedPartCount} physical parts loaded; {CandidateCount} drawing candidates prepared.");
                if (preparation.ExcludedPartCount > 0) AddActivity($"{preparation.ExcludedPartCount} selected parts were excluded because PART_POS was blank.");
                if (!HasPreparedCandidates) ShowMessage?.Invoke("Selected Parts", "No supported parts are currently selected in Tekla.");
                NotifyCounts();
            }
            catch (Exception ex) { HandleError("Selected parts could not be loaded.", ex); }
            finally { IsBusy = false; }
        }

        private async Task RunDryRunAsync()
        {
            IsBusy = true;
            try
            {
                var results = new List<DrawingProcessResult>();
                ProgressValue = 0;
                ProgressMaximum = Candidates.Count;
                foreach (var candidate in Candidates.Select(row => row.Candidate))
                {
                    CurrentPieceMark = candidate.PieceMark;
                    results.Add(_service.ProcessDryRunCandidate(candidate));
                    ProgressValue++;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                _dryRunResults = _service.CompleteDryRun(results);
                var byMark = _dryRunResults.ToDictionary(result => result.Candidate.PieceMark, StringComparer.OrdinalIgnoreCase);
                foreach (var row in Candidates) row.ApplyResult(byMark[row.PieceMark]);
                DryRunComplete = true;
                AddActivity($"Dry run completed — Ready: {ReadyCount} | Existing: {ExistingCount} | Review: {NeedReviewCount} | Failed: {FailedCount}");
                NotifyCounts();
                CandidatesView.Refresh();
            }
            catch (Exception ex) { HandleError("Dry run could not be completed.", ex); }
            finally { IsBusy = false; CurrentPieceMark = "—"; }
        }

        private bool CanGenerate()
        {
            return !IsBusy && DryRunComplete && CheckedReadyCount > 0 && TryGetBatchLimit(out _);
        }

        private async Task GenerateAsync()
        {
            if (!TryGetBatchLimit(out int limit)) return;
            BatchPlan plan;
            try
            {
                plan = _service.BuildBatchPlan(_dryRunResults, Candidates.Where(row => row.IsSelected).Select(row => row.PieceMark), limit);
            }
            catch (Exception ex) { HandleError("The controlled batch could not be prepared.", ex); return; }
            if (plan.Candidates.Count == 0) return;
            if (ConfirmBatch != null && !ConfirmBatch(plan)) return;

            IsBusy = true;
            ProgressValue = 0;
            ProgressMaximum = plan.Candidates.Count;
            var results = new List<DrawingProcessResult>();
            var stopwatch = Stopwatch.StartNew();
            AddActivity($"Batch started — {plan.Candidates.Count} drawings.");
            try
            {
                foreach (var candidate in plan.Candidates)
                {
                    CurrentPieceMark = candidate.PieceMark;
                    var result = _service.ProcessCandidate(candidate);
                    results.Add(result);
                    var row = Candidates.First(item => string.Equals(item.PieceMark, candidate.PieceMark, StringComparison.OrdinalIgnoreCase));
                    row.ApplyResult(result);
                    if (!string.IsNullOrWhiteSpace(result.TechnicalDetails)) TechnicalDetails = result.TechnicalDetails;
                    ProgressValue++;
                    AddActivity($"{candidate.PieceMark} — {result.Status}: {result.Message}");
                    NotifyCounts();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                stopwatch.Stop();
                var summary = _service.CompleteBatch(results, stopwatch.Elapsed);
                ResultSummary = $"Batch completed — Requested: {summary.Requested} | Created: {summary.Created} | Skipped: {summary.Skipped} | Failed: {summary.Failed} | Duration: {summary.Duration:hh\\:mm\\:ss}";
                AddActivity(ResultSummary);
            }
            catch (Exception ex) { HandleError("The batch stopped because of an unexpected orchestration error.", ex); }
            finally
            {
                IsBusy = false;
                CurrentPieceMark = "—";
                CandidatesView.Refresh();
                RaiseCommandStates();
            }
        }

        private bool FilterCandidate(object item)
        {
            var row = item as CandidateRowViewModel;
            if (row == null) return false;
            bool searchMatches = string.IsNullOrWhiteSpace(SearchText) || (row.PieceMark ?? string.Empty).IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            bool statusMatches = SelectedStatus == "All" || string.Equals(row.StatusText, SelectedStatus, StringComparison.OrdinalIgnoreCase);
            return searchMatches && statusMatches;
        }

        private bool TryGetBatchLimit(out int limit) => int.TryParse(BatchLimit, out limit) && limit > 0;
        private void CandidatePropertyChanged(object sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(CandidateRowViewModel.IsSelected)) { OnPropertyChanged(nameof(CheckedReadyCount)); RaiseCommandStates(); } }
        private void AddActivity(string message) { Activity.Add($"{DateTime.Now:HH:mm:ss}  {message}"); }
        private void HandleError(string message, Exception ex) { TechnicalDetails = ex.ToString(); AddActivity(message); ShowMessage?.Invoke("Single Part Automation", message); }
        private void NotifyCounts()
        {
            OnPropertyChanged(nameof(SelectedPartCount)); OnPropertyChanged(nameof(CandidateCount)); OnPropertyChanged(nameof(ReadyCount));
            OnPropertyChanged(nameof(ExistingCount)); OnPropertyChanged(nameof(NeedReviewCount)); OnPropertyChanged(nameof(FailedCount)); OnPropertyChanged(nameof(CheckedReadyCount));
            RaiseCommandStates();
        }
        private void RaiseCommandStates() { LoadCommand?.RaiseCanExecuteChanged(); DryRunCommand?.RaiseCanExecuteChanged(); GenerateCommand?.RaiseCanExecuteChanged(); }
        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null) { if (Equals(field, value)) return false; field = value; OnPropertyChanged(propertyName); return true; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
