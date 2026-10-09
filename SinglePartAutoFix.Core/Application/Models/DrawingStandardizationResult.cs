using SinglePartAutoFix.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Application.Models
{
    public class DrawingStandardizationResult
    {
        public DrawingStandardizationStatus Status { get; set; }
        public string Message { get; set; }

        public DrawingStandardizationResult(DrawingStandardizationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public static DrawingStandardizationResult NotConfigured()
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.NotConfigured, "Engineering Drawing standardization is not configured.");
        }

        public static DrawingStandardizationResult PendingCreation()
        {
            return new DrawingStandardizationResult(
                DrawingStandardizationStatus.PendingCreation,
                "Drawing standard verification will run after creation.");
        }

        public static DrawingStandardizationResult SettingsRequested(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.SettingsRequested, message);
        }

        public static DrawingStandardizationResult Verified(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.Verified, message);
        }

        public static DrawingStandardizationResult NeedReview(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.NeedReview, message);
        }

        public static DrawingStandardizationResult Failed(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.Failed, message);
        }

        public static DrawingStandardizationResult NotApplicable(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.NotApplicable, message);
        }
    }
}
