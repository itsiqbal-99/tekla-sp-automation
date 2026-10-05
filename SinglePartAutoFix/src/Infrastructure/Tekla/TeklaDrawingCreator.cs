using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Domain.Models;
using System;
using Tekla.Structures;
using Tekla.Structures.Drawing;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingCreator : IDrawingCreator
    {
        private readonly TeklaModelSession _tekla;

        public TeklaDrawingCreator(TeklaModelSession tekla)
        {
            _tekla = tekla;
        }

        public bool Create(DrawingCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!_tekla.IsConnected())
            {
                Console.WriteLine("Tekla Drawing API is not Connected");
                return false;
            }

            var drawingHandler = new DrawingHandler();
            if (!drawingHandler.GetConnectionStatus())
            {
                Console.WriteLine("Tekla Drawing API is not Connected");
                return false;
            }

            var partIdentifier = new Identifier(candidate.RepresentativePartId);
            var drawing = new SinglePartDrawing(partIdentifier, "SP_TEST_STANDARD");

            return drawing.Insert();

        }

    }
}
