using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Tekla.Structures.Drawing;

using ModelPart = Tekla.Structures.Model.Part;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingChecker: IDrawingChecker
    {
        private readonly TeklaModelSession _tekla;
        private HashSet<string> _existingPiecemarks;

        public TeklaDrawingChecker(TeklaModelSession tekla)
        {
            _tekla = tekla;
        }

        public bool Exists(DrawingCandidate candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            if(_existingPiecemarks == null)
            {
                LoadExistingPiecemark();
            }

            return _existingPiecemarks.Contains(candidate.PieceMark);

         }

        private void LoadExistingPiecemark()
        {
            _existingPiecemarks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var drawingHandler = new DrawingHandler();
            if(!drawingHandler.GetConnectionStatus())
            {
                throw new InvalidOperationException(
                    "Drawing API is not connected");
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
                part.GetReportProperty("PART_POS", ref pieceMark);

                if(!string.IsNullOrEmpty(pieceMark))
                {
                    _existingPiecemarks.Add(pieceMark);
                }
            }
        }

        public void MarkAsExisting(string pieceMark)
        {
            if (string.IsNullOrWhiteSpace(pieceMark))
                return;

            if(_existingPiecemarks == null)
            {
                LoadExistingPiecemark();
            }

            _existingPiecemarks.Add(pieceMark);
        }
    }

}
    
