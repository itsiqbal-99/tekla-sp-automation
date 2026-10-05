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
        public bool IsValid => Status == DrawingStandardValidationStatus.Ready;
        public DrawingStandardValidationResult(
            DrawingStandardValidationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public static DrawingStandardValidationResult Create(DrawingStandardValidationStatus status, string message)
        {
            return new DrawingStandardValidationResult(status, message);
        }


    }
}
