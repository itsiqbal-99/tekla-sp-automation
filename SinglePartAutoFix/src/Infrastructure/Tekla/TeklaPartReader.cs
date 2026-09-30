using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Application.Interfaces;
using Tekla.Structures.Model;
using System.Collections.Generic;
using System;
using System.Collections;
using Tekla.Structures.Model.Operations;
using SinglePartAutoFix.Application.Models;

using TeklaModelObjectSelector = Tekla.Structures.Model.ModelObjectSelector;

using TeklaUiModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaPartReader : IPartReader
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
            var result = new List<PartInfo>();

            if (!_tekla.IsConnected())
            {
                throw new InvalidOperationException("No active Tekla model connection is available.");
            }


            ModelObjectEnumerator objects;

            switch (query.SelectionMode)
            {
                case PartSelectionMode.Selected:

                    var uiSelector = new TeklaUiModelObjectSelector();

                    objects = uiSelector.GetSelectedObjects();

                    break;

                case PartSelectionMode.All:

                    var modelSelector = _tekla.GetModel().GetModelObjectSelector();

                    objects = modelSelector.GetAllObjectsWithType(
                            new[] { typeof(Part) }
                        );

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            while (objects.MoveNext())
            {
                if (!(objects.Current is Part part))
                    continue;

                var names = new ArrayList
                {
                    "PART_POS"
                };

                var values = new Hashtable();


                part.GetStringReportProperties(
                    names,
                    ref values
                );

                string pieceMark = "";

                if (values.ContainsKey("PART_POS"))
                {
                    pieceMark =
                        values["PART_POS"]?.ToString() ?? "";
                }

                string materialType = "";
                part.GetReportProperty("MATERIAL_TYPE", ref materialType);

                bool numberingUpToDate =
                    Operation.IsNumberingUpToDate(part);

                result.Add(new PartInfo
                {
                    Id = part.Identifier.ID,
                    PieceMark = pieceMark,
                    Profile = part.Profile.ProfileString,
                    Material = part.Material.MaterialString,
                    MaterialType = materialType,
                    isNumberingUpToDate = numberingUpToDate,
                });
            }

            return result;
        }
    }
}
