using SinglePartAutoFix.Domain.Models;

namespace SinglePartAutoFix.src.Application.Interfaces
{
    public interface IDrawingChecker
    {
        SinglePartAutoFix.Application.Models.DrawingLookupResult Find(
            DrawingCandidate candidate,
            bool forceRefresh = false);
        void Refresh();
    }
}
