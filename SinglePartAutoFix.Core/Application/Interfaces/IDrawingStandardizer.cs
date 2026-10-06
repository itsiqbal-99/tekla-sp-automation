using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Application.Interfaces
{
    public interface IDrawingStandardizer
    {
        DrawingStandardizationResult Apply(DrawingCandidate candidate);
    }
}
