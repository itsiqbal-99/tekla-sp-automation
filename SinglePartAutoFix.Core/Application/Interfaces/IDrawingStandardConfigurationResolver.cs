using SinglePartAutoFix.Application.Models;

namespace SinglePartAutoFix.Application.Interfaces
{
    public interface IDrawingStandardConfigurationResolver
    {
        bool CanResolve(DrawingStandardProfile profile);
    }
}
