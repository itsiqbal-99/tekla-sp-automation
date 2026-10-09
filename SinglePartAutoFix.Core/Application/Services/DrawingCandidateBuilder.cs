using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SinglePartAutoFix.Application.Services
{
    public class DrawingCandidateBuilder
    {
        public List<DrawingCandidate> Build(IReadOnlyList<PartInfo> parts)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            var candidates = parts
                .Where(part => !string.IsNullOrWhiteSpace(part.PieceMark))
                .GroupBy(
                    part => part.PieceMark.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var representative = group.First();
                    var groupedParts = group.ToList();
                    var validationIssues = new List<string>();

                    AddInconsistencyIssue(
                        validationIssues,
                        groupedParts.Select(part => part.Profile),
                        "profile");
                    AddInconsistencyIssue(
                        validationIssues,
                        groupedParts.Select(part => part.Material),
                        "material");
                    AddInconsistencyIssue(
                        validationIssues,
                        groupedParts.Select(part => part.MaterialType),
                        "material type");

                    return new DrawingCandidate
                    {
                        RepresentativePartId = representative.Id,
                        ModelPartIds = groupedParts.Select(part => part.Id).ToList(),
                        PieceMark = group.Key,
                        Profile = representative.Profile,
                        Material = representative.Material,
                        MaterialType = representative.MaterialType,
                        PartCount = groupedParts.Count,
                        IsNumberingUpToDate = groupedParts.All(part => part.isNumberingUpToDate),
                        ValidationMessage = validationIssues.Count == 0
                            ? string.Empty
                            : string.Join(" ", validationIssues)
                    };
                })
                .ToList();

            candidates.AddRange(parts
                .Where(part => string.IsNullOrWhiteSpace(part.PieceMark))
                .Select(part => new DrawingCandidate
                {
                    RepresentativePartId = part.Id,
                    ModelPartIds = new List<int> { part.Id },
                    PieceMark = string.Empty,
                    Profile = part.Profile,
                    Material = part.Material,
                    MaterialType = part.MaterialType,
                    PartCount = 1,
                    IsNumberingUpToDate = part.isNumberingUpToDate,
                    ValidationMessage = "PART_POS is missing. Update numbering or the part properties, then select the parts again."
                }));

            return candidates
                .OrderBy(candidate => candidate.PieceMarkDisplay, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddInconsistencyIssue(
            ICollection<string> issues,
            IEnumerable<string> values,
            string fieldName)
        {
            int distinctValues = values
                .Select(value => (value ?? string.Empty).Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctValues > 1)
            {
                issues.Add($"Parts with this PART_POS have inconsistent {fieldName} values.");
            }
        }
    }
}
