using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Domain.Models;
using System;
using Tekla.Structures;
using Tekla.Structures.Drawing;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingCreator: IDrawingCreator
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

            if(!_tekla.IsConnected())
            {
                throw new InvalidOperationException("No active Tekla model connection is available.");
            }

            var drawingHandler = new DrawingHandler();
            if(!drawingHandler.GetConnectionStatus())
            {
                throw new InvalidOperationException("Tekla Drawing API is not connected.");
            }

            var partIdentifier = new Identifier(candidate.RepresentativePartId);
            var drawing = new SinglePartDrawing(partIdentifier);

            return drawing.Insert();

        }

    }
}
