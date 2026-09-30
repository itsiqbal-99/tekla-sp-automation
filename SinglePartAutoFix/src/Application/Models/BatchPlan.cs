using SinglePartAutoFix.Domain.Models;
using System.Collections.Generic;

namespace SinglePartAutoFix.Application.Models
{
    public sealed class BatchPlan
    {
        public int SelectedReadyCount { get; set; }
        public int BatchLimit { get; set; }
        public IReadOnlyList<DrawingCandidate> Candidates { get; set; }
    }
}
