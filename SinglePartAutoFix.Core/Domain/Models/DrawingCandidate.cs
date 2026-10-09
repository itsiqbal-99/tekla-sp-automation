using System;
using System.Collections.Generic;

namespace SinglePartAutoFix.Domain.Models
{
    public class DrawingCandidate
    {
        public int RepresentativePartId { get; set; }
        public IReadOnlyList<int> ModelPartIds { get; set; } = new List<int>();
        public string PieceMark { get; set; }
        public string Profile { get; set; }
        public string Material { get; set; }
        public string MaterialType { get; set; }
        public int PartCount { get; set; }
        public bool IsNumberingUpToDate { get; set; }
        public string ValidationMessage { get; set; }

        public bool HasValidationIssue => !string.IsNullOrWhiteSpace(ValidationMessage);

        public string PieceMarkDisplay => string.IsNullOrWhiteSpace(PieceMark)
            ? $"Missing PART_POS (ID {RepresentativePartId})"
            : PieceMark;
    }
}
