using SinglePartAutoFix.Domain.Models;
using System;

namespace SinglePartAutoFix.Application.Models
{
    public class DrawingProcessResult
    {
        public DrawingCandidate Candidate { get; set; }
        public DrawingProcessStatus Status { get; set; }
        public String Message { get; set; }
        public DrawingStandardizationResult Standardization { get; set; }
        public DrawingLookupResult DrawingLookup { get; set; }
        public DrawingVerificationResult Verification { get; set; }
        public string SuggestedAction { get; set; }
        public string OperationId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public long DurationMilliseconds { get; set; }
        public string TechnicalError { get; set; }

        public string DrawingStateDisplay => DrawingLookup?.Drawing?.StateDisplay ?? "-";
        public string DrawingScaleDisplay => DrawingLookup?.Drawing?.ScaleDisplay ?? "-";

        public string DetailDisplay
        {
            get
            {
                string action = string.IsNullOrWhiteSpace(SuggestedAction)
                    ? string.Empty
                    : $" Suggested action: {SuggestedAction}";
                string teklaState = string.IsNullOrWhiteSpace(DrawingLookup?.Drawing?.UpToDateStatus)
                    ? string.Empty
                    : $" Tekla status: {DrawingLookup.Drawing.UpToDateStatus}.";
                string dates = DrawingLookup?.Drawing == null
                    ? string.Empty
                    : $" Created: {DrawingLookup.Drawing.CreationDate:yyyy-MM-dd HH:mm}; " +
                      $"modified: {DrawingLookup.Drawing.ModificationDate:yyyy-MM-dd HH:mm}.";
                return $"{Message}{action}{teklaState}{dates}";
            }
        }

        public string StandardizationDisplay
        {
            get
            {
                if (Standardization != null)
                {
                    return Standardization.Status.ToString();
                }

                if (Status == DrawingProcessStatus.ReadyToCreate)
                {
                    return DrawingStandardizationStatus.PendingCreation.ToString();
                }

                if (Status == DrawingProcessStatus.Existing)
                {
                    return DrawingStandardizationStatus.NotApplicable.ToString();
                }

                if (Status == DrawingProcessStatus.NeedReview)
                {
                    return DrawingStandardizationStatus.NotApplicable.ToString();
                }

                return "-";
            }
        }

    }
}
