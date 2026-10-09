using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Core.Infrastructure.Tekla;
using System;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingStandardConfigurationValidator
    {
        private readonly TeklaModelSession _tekla;
        private readonly TeklaDrawingStandardConfigurationResolver _configurationResolver;

        public TeklaDrawingStandardConfigurationValidator(
            TeklaModelSession tekla,
            TeklaDrawingStandardConfigurationResolver configurationResolver)
        {
            _tekla = tekla ?? throw new ArgumentNullException(nameof(tekla));
            _configurationResolver = configurationResolver ??
                throw new ArgumentNullException(nameof(configurationResolver));
        }

        public DrawingStandardValidationResult Validate(DrawingStandardProfile profile)
        {
            if (profile == null)
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.NotConfigured,
                    "Drawing standard profile is not configured.");
            }

            if (!profile.IsEnabled)
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.Disabled,
                    "Drawing standard profile is disabled.");
            }

            if (!_tekla.IsConnected())
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.TeklaNotConnected,
                    "Tekla Structures is not connected.");
            }

            if (string.IsNullOrWhiteSpace(profile.DrawingAttributeName))
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.ConfigurationMissing,
                    "Drawing attribute name is not configured.");
            }

            string resolvedPath;
            string errorMessage;
            if (!_configurationResolver.TryResolve(profile, out resolvedPath, out errorMessage))
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.ConfigurationMissing,
                    errorMessage);
            }

            return DrawingStandardValidationResult.Create(
                DrawingStandardValidationStatus.Ready,
                $"Drawing standard profile '{profile.Name}' v{profile.Version} is ready.",
                resolvedPath);
        }
    }
}
