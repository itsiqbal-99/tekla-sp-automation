using SinglePartAutoFix.Application.Models;
using System.Collections.Generic;

namespace SinglePartAutoFix.Application.Configuration
{
    public class DrawingStandardConfiguration
    {
        public static IReadOnlyList<DrawingStandardProfile> CreateProfile()
        {
            return new List<DrawingStandardProfile>
            {
            new DrawingStandardProfile(
                id: "single-part-test",
                name: "Single Part Test Standard",
                drawingAttributeName: "SP_TEST_STANDARD",
                version: "1.0",
                isEnabled: true,
                requiredAttributeFileName: "SP_TEST_STANDARD.wd",
                expectedViewScale: 5.0)
            };

            //return new List<DrawingStandardProfile>();
        }
    }
}
