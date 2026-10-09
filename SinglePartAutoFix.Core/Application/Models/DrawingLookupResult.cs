namespace SinglePartAutoFix.Application.Models
{
    public class DrawingLookupResult
    {
        public DrawingLookupStatus Status { get; set; }
        public int DrawingCount { get; set; }
        public DrawingSnapshot Drawing { get; set; }
        public string Message { get; set; }

        public bool Exists => Status == DrawingLookupStatus.Found;

        public static DrawingLookupResult NotFound()
        {
            return new DrawingLookupResult
            {
                Status = DrawingLookupStatus.NotFound,
                DrawingCount = 0,
                Message = "No Single Part Drawing was found."
            };
        }
    }
}
