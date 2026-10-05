using SinglePartAutoFix.Application.Models;
using System.Collections.Generic;

namespace SinglePartAutoFix.Application.Interfaces
{
    public interface IDrawingStandardProfileProvider
    {
        IReadOnlyList<DrawingStandardProfile> GetProfiles();
        DrawingStandardProfile GetById(string id);
    }
}
