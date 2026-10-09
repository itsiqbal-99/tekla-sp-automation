using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using TeklaUiModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaPartReader
    {
        private readonly TeklaModelSession _tekla;

        public TeklaPartReader(TeklaModelSession tekla)
        {
            _tekla = tekla;
        }

        public IReadOnlyList<PartInfo> GetParts(PartQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (!_tekla.IsConnected())
            {
                throw new InvalidOperationException(
                    "Tekla Structures is not connected. Reconnect and select the parts again.");
            }

            ModelObjectEnumerator objects;
            if (query.SelectionMode == PartSelectionMode.Selected)
            {
                objects = new TeklaUiModelObjectSelector().GetSelectedObjects();
            }
            else if (query.SelectionMode == PartSelectionMode.All)
            {
                objects = _tekla.GetModel()
                    .GetModelObjectSelector()
                    .GetAllObjectsWithType(new[] { typeof(Part) });
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }

            var result = new List<PartInfo>();
            while (objects.MoveNext())
            {
                var part = objects.Current as Part;
                if (part == null)
                {
                    continue;
                }

                string pieceMark = string.Empty;
                string materialType = string.Empty;
                part.GetReportProperty("PART_POS", ref pieceMark);
                part.GetReportProperty("MATERIAL_TYPE", ref materialType);

                result.Add(new PartInfo
                {
                    Id = part.Identifier.ID,
                    PieceMark = pieceMark,
                    Profile = part.Profile.ProfileString,
                    Material = part.Material.MaterialString,
                    MaterialType = materialType,
                    isNumberingUpToDate = Operation.IsNumberingUpToDate(part)
                });
            }

            return result;
        }
    }
}
