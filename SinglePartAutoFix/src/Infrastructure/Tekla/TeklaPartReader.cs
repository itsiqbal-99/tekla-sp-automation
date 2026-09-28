using SinglePartAutoFix.Domain.Models;
using Tekla.Structures.Model;
using System.Collections.Generic;
using System;
using System.Collections;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaPartReader
    {
        private readonly TeklaModelSession _tekla;

        public TeklaPartReader(TeklaModelSession tekla)
        {
            _tekla = tekla;
        }

        public IReadOnlyList<PartInfo> GetParts()
        {
            var result = new List<PartInfo>();

            if(!_tekla.IsConnected())
            {
                Console.WriteLine("ERROR DI READONLY");
                return result;
            }


            var selector = _tekla.GetModel().GetModelObjectSelector();
            var objects = selector.GetAllObjectsWithType(new[] {typeof(Part)});

            while(objects.MoveNext())
            {
                if (objects.Current is Part part)
                {
                    var names = new ArrayList { "PART_POS" };
                    var values = new Hashtable();

                    part.GetStringReportProperties(names, ref values);
                    string pieceMark = "";
                    if (values.ContainsKey("PART_POS"))
                    {
                        pieceMark = values["PART_POS"]?.ToString() ?? "";
                    }

                    result.Add(new PartInfo
                    {
                        Id = part.Identifier.ID,
                        PieceMark = pieceMark,
                        Profile = part.Profile.ProfileString,
                        Material = part.Material.MaterialString
                    });

                    if (result.Count % 100 == 0)
                    {
                        Console.WriteLine( $"Read {result.Count} parts...");
                    }
                }
            }
            return result;
        }
    }
}
