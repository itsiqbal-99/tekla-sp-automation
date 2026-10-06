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
                    return "Pending Creation";
                }

                if (Status == DrawingProcessStatus.Existing)
                {
                    return "Not Checked";
                }

                if (Status == DrawingProcessStatus.NeedReview)
                {
                    return "Not Applicable";
                }

                return "-";
            }
        }

    }
}
