using SinglePartAutoFix.Domain.Models;

namespace SinglePartAutoFix.src.Application.Interfaces
{
    public interface IDrawingChecker
    {
        bool Exists(DrawingCandidate candidate);
    }
}
