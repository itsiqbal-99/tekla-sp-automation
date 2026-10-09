using System;
using System.Collections.Generic;
using System.Linq;

namespace SinglePartAutoFix.Application.Models
{
    public class DrawingSnapshot
    {
        public int PartIdentifier { get; set; }
        public string PieceMark { get; set; }
        public string Name { get; set; }
        public string Mark { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime ModificationDate { get; set; }
        public string UpToDateStatus { get; set; }
        public bool IsLocked { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsIssued { get; set; }
        public bool IsIssuedButModified { get; set; }
        public bool IsReadyForIssue { get; set; }
        public IReadOnlyList<double> ViewScales { get; set; } = new List<double>();

        public int ViewCount => ViewScales?.Count ?? 0;

        public string StateDisplay
        {
            get
            {
                var flags = new List<string>();
                if (IsLocked) flags.Add("Locked");
                if (IsFrozen) flags.Add("Frozen");
                if (IsIssuedButModified) flags.Add("Issued / Modified");
                else if (IsIssued) flags.Add("Issued");

                if (!string.IsNullOrWhiteSpace(UpToDateStatus))
                {
                    flags.Add(ToFriendlyStatus(UpToDateStatus));
                }

                return flags.Count == 0 ? "Available" : string.Join(" · ", flags);
            }
        }

        public string ScaleDisplay => ViewCount == 0
            ? "No views"
            : string.Join(", ", ViewScales.Select(scale => $"1:{scale:0.##}"));

        private static string ToFriendlyStatus(string value)
        {
            switch (value)
            {
                case "DrawingIsUpToDate": return "Up to date";
                case "PartsWereModified": return "Model changed";
                case "DrawingIsUpToDateButMayNeedChecking": return "Needs checking";
                case "OriginalPartDeleted": return "Model changed";
                case "AllPartsDeleted": return "Model changed";
                default: return "Needs checking";
            }
        }
    }
}
