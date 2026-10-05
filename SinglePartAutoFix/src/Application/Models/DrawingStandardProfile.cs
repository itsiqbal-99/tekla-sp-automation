using System;

namespace SinglePartAutoFix.Application.Models
{
    public class DrawingStandardProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DrawingAttributeName { get; set; }
        public string Version { get; set; }
        public bool IsEnabled { get; set; }

        public DrawingStandardProfile(string id, string name, string drawingAttributeName, string version, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Drawing standard profile ID is required.",
                    nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Drawing standard profile name is required.",
                    nameof(name));
            }

            if (string.IsNullOrWhiteSpace(drawingAttributeName))
            {
                throw new ArgumentException(
                    "Drawing attribute name is required.",
                    nameof(drawingAttributeName));
            }

            Id = id;
            Name = name;
            DrawingAttributeName = drawingAttributeName;
            Version = string.IsNullOrWhiteSpace(version)
                ? "1.0"
                : version;
            IsEnabled = isEnabled;
        }
    }
}

