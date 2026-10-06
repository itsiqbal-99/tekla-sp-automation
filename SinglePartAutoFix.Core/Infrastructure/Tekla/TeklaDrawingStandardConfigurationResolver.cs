using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.IO;
using System.Linq;

namespace SinglePartAutoFix.Core.Infrastructure.Tekla
{
    public class TeklaDrawingStandardConfigurationResolver : IDrawingStandardConfigurationResolver
    {

        private readonly TeklaModelSession _tekla;

        public TeklaDrawingStandardConfigurationResolver(TeklaModelSession tekla)
        {
            _tekla = tekla ?? throw new ArgumentNullException(nameof(tekla));
        }

        public bool CanResolve(
            DrawingStandardProfile profile)
        {
            if (profile == null ||
                !profile.IsEnabled ||
                string.IsNullOrWhiteSpace(
                    profile.DrawingAttributeName))
            {
                return false;
            }

            if (!_tekla.IsConnected())
            {
                return false;
            }

            var modelPath = _tekla.GetModelPath();

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return false;
            }

            var attributesPath = Path.Combine(modelPath, "attributes");

            if (!Directory.Exists(attributesPath))
            {
                return false;
            }

            var attributeName = profile.DrawingAttributeName;

            return Directory.EnumerateFiles(
                    attributesPath,
                    attributeName + ".*",
                    SearchOption.TopDirectoryOnly)
                .Any();
        }

    }
}
