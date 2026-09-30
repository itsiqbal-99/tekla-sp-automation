using SinglePartAutoFix.Domain.Models;
using System.Collections.Generic;

namespace SinglePartAutoFix.Application.Models
{
    public sealed class PreparationResult
    {
        public int SelectedPartCount { get; set; }
        public int ExcludedPartCount { get; set; }
        public IReadOnlyList<DrawingCandidate> Candidates { get; set; }
    }
}
