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

        public static DrawingStandardizationResult Applied(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.Applied, message);
        }

        public static DrawingStandardizationResult Failed(string message)
        {
            return new DrawingStandardizationResult(DrawingStandardizationStatus.Failed, message);
        }
    }
}
