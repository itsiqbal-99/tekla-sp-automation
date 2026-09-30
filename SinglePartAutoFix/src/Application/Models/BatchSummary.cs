using System;
using System.Collections.Generic;
using System.Linq;

namespace SinglePartAutoFix.Application.Models
{
    public sealed class BatchSummary
    {
        public IReadOnlyList<DrawingProcessResult> Results { get; set; }
        public TimeSpan Duration { get; set; }
        public int Requested { get { return Results == null ? 0 : Results.Count; } }
        public int Created { get { return Count(DrawingProcessStatus.Created); } }
        public int Existing { get { return Count(DrawingProcessStatus.Existing); } }
        public int NeedReview { get { return Count(DrawingProcessStatus.NeedReview); } }
        public int Skipped { get { return Existing + NeedReview; } }
        public int Failed { get { return Count(DrawingProcessStatus.Failed); } }

        private int Count(DrawingProcessStatus status)
        {
            return Results == null ? 0 : Results.Count(result => result.Status == status);
        }
    }
}
