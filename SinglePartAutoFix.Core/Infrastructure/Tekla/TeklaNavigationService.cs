using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;
using ModelPart = Tekla.Structures.Model.Part;
using TeklaUiModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaNavigationService
    {
        private readonly TeklaModelSession _tekla;

        public TeklaNavigationService(TeklaModelSession tekla)
        {
            _tekla = tekla ?? throw new ArgumentNullException(nameof(tekla));
        }

        public bool IsDrawingApiConnected()
        {
            return new DrawingHandler().GetConnectionStatus();
        }

        public bool FocusInModel(DrawingCandidate candidate, out string message)
        {
            if (candidate == null)
            {
                message = "Select a drawing candidate first.";
                return false;
            }

            if (!_tekla.IsConnected())
            {
                message = "Reconnect to Tekla Structures, then try again.";
                return false;
            }

            var objects = new ArrayList();
            IEnumerable<int> ids = candidate.ModelPartIds == null || candidate.ModelPartIds.Count == 0
                ? new[] { candidate.RepresentativePartId }
                : candidate.ModelPartIds;

            foreach (int id in ids)
            {
                var modelObject = _tekla.GetModel().SelectModelObject(new Identifier(id));
                if (modelObject != null)
                {
                    objects.Add(modelObject);
                }
            }

            if (objects.Count == 0)
            {
                message = "The selected model parts no longer exist. Run Select Parts again.";
                return false;
            }

            if (!new TeklaUiModelObjectSelector().Select(objects))
            {
                message = "Tekla could not focus the model parts. Run Select Parts again.";
                return false;
            }

            message = $"Focused {objects.Count} model part(s) for {candidate.PieceMarkDisplay}.";
            return true;
        }

        public bool OpenDrawing(DrawingCandidate candidate, out string message)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.PieceMark))
            {
                message = "This candidate does not have a drawing identity.";
                return false;
            }

            var drawingHandler = new DrawingHandler();
            if (!drawingHandler.GetConnectionStatus())
            {
                message = "Reconnect to the Tekla Drawing API, then try again.";
                return false;
            }

            List<Drawing> matches = FindDrawings(candidate.PieceMark, drawingHandler);
            if (matches.Count == 0)
            {
                message = "No Single Part Drawing was found. Refresh the dry check and try again.";
                return false;
            }

            if (matches.Count > 1)
            {
                message = "More than one drawing uses this PART_POS. Resolve the duplicates in Tekla first.";
                return false;
            }

            Drawing target = matches[0];
            Drawing active = drawingHandler.GetActiveDrawing();
            if (active != null)
            {
                if (active.IsSameDatabaseObject(target))
                {
                    message = "The drawing is already active in Tekla.";
                    return true;
                }

                message = "Another drawing is active in Tekla. Save or close it in Tekla, then try again.";
                return false;
            }

            bool opened = drawingHandler.SetActiveDrawing(target);
            message = opened
                ? $"Opened drawing for {candidate.PieceMark}."
                : "Tekla could not open the drawing.";
            return opened;
        }

        private List<Drawing> FindDrawings(string pieceMark, DrawingHandler drawingHandler)
        {
            var matches = new List<Drawing>();
            var drawings = drawingHandler.GetDrawings();
            while (drawings.MoveNext())
            {
                var drawing = drawings.Current as SinglePartDrawing;
                if (drawing == null)
                {
                    continue;
                }

                var part = _tekla.GetModel().SelectModelObject(drawing.PartIdentifier) as ModelPart;
                if (part == null)
                {
                    continue;
                }

                string drawingPieceMark = string.Empty;
                part.GetReportProperty("PART_POS", ref drawingPieceMark);
                if (string.Equals(
                    drawingPieceMark?.Trim(),
                    pieceMark.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(drawing);
                }
            }

            return matches;
        }
    }
}
