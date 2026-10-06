using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Application.Services
{
    public class DrawingCandidateBuilder
    {
        public List<DrawingCandidate> Build(IReadOnlyList<PartInfo> parts)
        {
            return parts
                .Where(part => !string.IsNullOrWhiteSpace(part.PieceMark))
                .GroupBy(part => part.PieceMark)
                .Select(group =>
                {
                    var representative = group.First();

                    return new DrawingCandidate
                    {
                        RepresentativePartId = representative.Id,
                        PieceMark = representative.PieceMark,
                        Profile = representative.Profile,
                        Material = representative.Material,
                        MaterialType = representative.MaterialType,
                        PartCount = group.Count(),
                        IsNumberingUpToDate = group.All(part => part.isNumberingUpToDate)
                    };
                })
                .ToList();
        }
    }
}
