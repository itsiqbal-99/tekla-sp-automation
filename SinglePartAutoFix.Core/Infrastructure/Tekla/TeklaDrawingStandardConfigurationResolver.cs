using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.Collections.Generic;
using System.IO;
using Tekla.Structures;

namespace SinglePartAutoFix.Core.Infrastructure.Tekla
{
    public class TeklaDrawingStandardConfigurationResolver
    {
        private readonly TeklaModelSession _tekla;

        public TeklaDrawingStandardConfigurationResolver(TeklaModelSession tekla)
        {
            _tekla = tekla ?? throw new ArgumentNullException(nameof(tekla));
        }

        public bool TryResolve(
            DrawingStandardProfile profile,
            out string resolvedPath,
            out string errorMessage)
        {
            resolvedPath = string.Empty;
            errorMessage = string.Empty;

            if (profile == null ||
                !profile.IsEnabled ||
                string.IsNullOrWhiteSpace(profile.RequiredAttributeFileName))
            {
                errorMessage = "Drawing standard profile or required attribute filename is not configured.";
                return false;
            }

            if (!_tekla.IsConnected())
            {
                errorMessage = "Tekla Structures is not connected.";
                return false;
            }

            string modelPath = _tekla.GetModelPath();
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                errorMessage = "The active model path is not available.";
                return false;
            }

            var searchPaths = new List<string> { Path.Combine(modelPath, "attributes") };
            AddAdvancedOptionPaths(searchPaths, "XS_PROJECT");
            AddAdvancedOptionPaths(searchPaths, "XS_FIRM");
            AddAdvancedOptionPaths(searchPaths, "XS_SYSTEM");

            foreach (string searchPath in searchPaths)
            {
                if (string.IsNullOrWhiteSpace(searchPath) || !Directory.Exists(searchPath))
                {
                    continue;
                }

                string candidatePath = Path.Combine(searchPath, profile.RequiredAttributeFileName);
                if (File.Exists(candidatePath))
                {
                    resolvedPath = candidatePath;
                    return true;
                }
            }

            errorMessage =
                $"Required Tekla attribute file '{profile.RequiredAttributeFileName}' was not found " +
                "in the model, project, firm, or system attribute paths.";
            return false;
        }

        private static void AddAdvancedOptionPaths(ICollection<string> searchPaths, string optionName)
        {
            try
            {
                List<string> paths;
                if (!TeklaStructuresSettings.GetAdvancedOptionPaths(optionName, out paths, null) ||
                    paths == null)
                {
                    return;
                }

                foreach (string path in paths)
                {
                    if (!string.IsNullOrWhiteSpace(path) && !searchPaths.Contains(path))
                    {
                        searchPaths.Add(path);
                    }
                }
            }
            catch
            {
                // Optional Tekla paths can be unavailable in some environments.
            }
        }
    }
}
