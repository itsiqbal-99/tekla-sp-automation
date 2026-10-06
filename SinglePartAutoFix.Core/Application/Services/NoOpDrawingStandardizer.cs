using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.src.Application.Services
{
    public class NoOpDrawingStandardizer : IDrawingStandardizer
    {
        public DrawingStandardizationResult Apply(DrawingCandidate candidate)
        {
         if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }
            return DrawingStandardizationResult.NotConfigured();
        }
    }
}
