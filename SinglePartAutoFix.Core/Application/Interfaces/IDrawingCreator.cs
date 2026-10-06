using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;

namespace SinglePartAutoFix.Application.Interfaces
{
    public interface IDrawingCreator
    {
        bool Create(
            DrawingCandidate candidate,
            DrawingStandardProfile standardProfile = null
            );
    }
}
