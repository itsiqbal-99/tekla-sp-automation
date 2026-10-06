using SinglePartAutoFix.Application.Models;

namespace SinglePartAutoFix.src.Application.Interfaces
{
    public interface IDrawingStandardConfigurationValidator
    {
        DrawingStandardValidationResult Validate(DrawingStandardProfile profile);
    }
}
