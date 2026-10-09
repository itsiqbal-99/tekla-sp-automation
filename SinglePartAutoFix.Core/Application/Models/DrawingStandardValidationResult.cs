namespace SinglePartAutoFix.Application.Models
{
    public enum DrawingStandardValidationStatus
    {
        NotConfigured,
        Disabled,
        TeklaNotConnected,
        ConfigurationMissing,
        Ready
    }
    public class DrawingStandardValidationResult
    {
        public DrawingStandardValidationStatus Status { get; set; }
        public string Message { get; set; }
        public string ResolvedPath { get; set; }
        public bool IsValid => Status == DrawingStandardValidationStatus.Ready;
        public DrawingStandardValidationResult(
            DrawingStandardValidationStatus status,
            string message,
            string resolvedPath = null)
        {
            Status = status;
            Message = message;
            ResolvedPath = resolvedPath ?? string.Empty;
        }

        public static DrawingStandardValidationResult Create(
            DrawingStandardValidationStatus status,
            string message,
            string resolvedPath = null)
        {
            return new DrawingStandardValidationResult(status, message, resolvedPath);
        }


    }
}
