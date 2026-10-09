namespace SinglePartAutoFix.Application.Models
{
    public class DrawingVerificationResult
    {
        public bool IsVerified { get; set; }
        public string CheckName { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public string Message { get; set; }
    }
}
