using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SinglePartAutoFix.Application.Services
{
    public class DrawingStandardProfileProvider : IDrawingStandardProfileProvider
    {
        private readonly IReadOnlyList<DrawingStandardProfile> _profiles;
        public DrawingStandardProfileProvider(IReadOnlyList<DrawingStandardProfile> profiles)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        }

        public IReadOnlyList<DrawingStandardProfile> GetProfiles()
        {
            return _profiles;
        }

        public DrawingStandardProfile GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return _profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

}
