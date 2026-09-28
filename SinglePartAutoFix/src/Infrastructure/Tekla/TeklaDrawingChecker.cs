using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using Tekla.Structures.Drawing;

using ModelPart = Tekla.Structures.Model.Part;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingChecker: IDrawingChecker
    {
        private readonly TeklaModelSession _tekla;

        public TeklaDrawingChecker(TeklaModelSession tekla)
        {
            _tekla = tekla;
        }

        public bool Exists(DrawingCandidate candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            var drawingHandler = new DrawingHandler();
            if(!drawingHandler.GetConnectionStatus())
            {
                return false;
            }
            var drawings = drawingHandler.GetDrawings();

            while (drawings.MoveNext())
            {
                if (!(drawings.Current is SinglePartDrawing drawing))
                    continue;

                var modelObject = _tekla.GetModel().SelectModelObject(drawing.PartIdentifier);

                if (!(modelObject is ModelPart part))
                    continue;

                string pieceMark = "";

                part.GetReportProperty(
                    "PART_POS",
                    ref pieceMark
                );

                if (string.Equals(
                    pieceMark,
                    candidate.PieceMark,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

            }

            return false;
         }

        public void PrintSinglePartDrawings()
        {
            var drawingHandler = new DrawingHandler();
            if (!drawingHandler.GetConnectionStatus())
            {
                Console.WriteLine("Drawing API not Connected.");
                return;
            }

            var drawings = drawingHandler.GetDrawings();

            int count = 0;
            while (drawings.MoveNext())
            {
                if (!(drawings.Current is SinglePartDrawing drawing))
                    continue;

                count++;

                string pieceMark = "";
                var modelObject = _tekla.GetModel().SelectModelObject(drawing.PartIdentifier);

                if(modelObject is ModelPart part)
                {
                    part.GetReportProperty("PART_POS", ref pieceMark);
                }

                Console.WriteLine(
                    $"Drawing #{count} | " +
                    $"Name: {drawing.Name} | " +
                    $"PART_POS: {pieceMark} | " +
                    $"Part ID: {drawing.PartIdentifier.ID}"
                );

            }

            Console.WriteLine();
            Console.WriteLine($"Total Single Part Drawbgs: {count}");
        }
    }

}
    
