using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SinglePartAutoFix.Wpf.ViewModels
{
    public sealed class CandidateRowViewModel : INotifyPropertyChanged
    {
        private DrawingProcessResult _result;
        private bool _isSelected;

        public CandidateRowViewModel(DrawingCandidate candidate) { Candidate = candidate; }
        public DrawingCandidate Candidate { get; }
        public string PieceMark => Candidate.PieceMark;
        public string Profile => Candidate.Profile;
        public string Material => Candidate.Material;
        public string MaterialType => Candidate.MaterialType;
        public int Quantity => Candidate.PartCount;
        public string Numbering => Candidate.IsNumberingUpToDate ? "Ready" : "Not ready";
        public DrawingProcessStatus? Status => _result?.Status;
        public string StatusText => Status.HasValue ? DisplayStatus(Status.Value) : "Pending";
        public string Message => _result?.Message ?? "Run Dry Run to classify this candidate.";
        public bool IsEligible => Status == DrawingProcessStatus.ReadyToCreate;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                bool next = IsEligible && value;
                if (_isSelected == next) return;
                _isSelected = next;
                OnPropertyChanged();
            }
        }

        public void ApplyResult(DrawingProcessResult result)
        {
            _result = result;
            if (!IsEligible) _isSelected = false;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(IsEligible));
            OnPropertyChanged(nameof(IsSelected));
        }

        private static string DisplayStatus(DrawingProcessStatus status)
        {
            switch (status)
            {
                case DrawingProcessStatus.ReadyToCreate: return "Ready";
                case DrawingProcessStatus.NeedReview: return "Need Review";
                default: return status.ToString();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
