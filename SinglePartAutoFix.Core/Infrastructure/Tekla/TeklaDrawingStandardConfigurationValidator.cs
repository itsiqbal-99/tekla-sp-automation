using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingStandardConfigurationValidator : IDrawingStandardConfigurationValidator
    {
        private readonly TeklaModelSession _tekla;
        private readonly IDrawingStandardConfigurationResolver _configurationResolver;
        public TeklaDrawingStandardConfigurationValidator(TeklaModelSession tekla, IDrawingStandardConfigurationResolver configurationResolver)
        {
            _tekla = tekla ?? throw new ArgumentNullException(nameof(tekla));
            _configurationResolver = configurationResolver ?? throw new ArgumentNullException(nameof(configurationResolver));
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
                    "Drawing attribute name is not configured"
                    );

            }

            if (!_configurationResolver.CanResolve(profile))
            {
                return DrawingStandardValidationResult.Create(
                    DrawingStandardValidationStatus.ConfigurationMissing,
                    "Tekla drawing configuration could not be resolved"
                    );
            }

            return DrawingStandardValidationResult.Create(
                DrawingStandardValidationStatus.Ready,
                "Drawing standard profile is ready.");
        }
    }
}
