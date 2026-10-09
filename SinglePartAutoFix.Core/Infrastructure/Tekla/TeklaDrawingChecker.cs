using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;

using ModelPart = Tekla.Structures.Model.Part;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaDrawingChecker : IDrawingChecker
    {
        private readonly TeklaModelSession _tekla;
        private Dictionary<string, List<DrawingSnapshot>> _drawingsByPieceMark;

        public TeklaDrawingChecker(TeklaModelSession tekla)
        {
            _tekla = tekla ?? throw new ArgumentNullException(nameof(tekla));
        }

        public DrawingLookupResult Find(DrawingCandidate candidate, bool forceRefresh = false)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (string.IsNullOrWhiteSpace(candidate.PieceMark))
            {
                return DrawingLookupResult.NotFound();
            }

            if (forceRefresh || _drawingsByPieceMark == null)
            {
                Refresh();
            }

            string key = Normalize(candidate.PieceMark);
            List<DrawingSnapshot> matches;
            if (!_drawingsByPieceMark.TryGetValue(key, out matches) || matches.Count == 0)
            {
                return DrawingLookupResult.NotFound();
            }

            if (matches.Count > 1)
            {
                return new DrawingLookupResult
                {
                    Status = DrawingLookupStatus.Duplicate,
                    DrawingCount = matches.Count,
                    Drawing = matches.FirstOrDefault(),
                    Message = $"{matches.Count} Single Part Drawings use PART_POS '{candidate.PieceMark}'."
                };
            }

            return new DrawingLookupResult
            {
                Status = DrawingLookupStatus.Found,
                DrawingCount = 1,
                Drawing = matches[0],
                Message = "A Single Part Drawing already exists."
            };
        }

        public void Refresh()
        {
            var refreshed = new Dictionary<string, List<DrawingSnapshot>>(
                StringComparer.OrdinalIgnoreCase);

            var drawingHandler = new DrawingHandler();
            if (!drawingHandler.GetConnectionStatus())
            {
                throw new InvalidOperationException("Tekla Drawing API is not connected.");
            }

            var drawings = drawingHandler.GetDrawings();
            while (drawings.MoveNext())
            {
                var drawing = drawings.Current as SinglePartDrawing;
                if (drawing == null)
                {
                    continue;
                }

                var modelObject = _tekla.GetModel().SelectModelObject(drawing.PartIdentifier);
                var part = modelObject as ModelPart;
                if (part == null)
                {
                    continue;
                }

                string pieceMark = string.Empty;
                part.GetReportProperty("PART_POS", ref pieceMark);
                if (string.IsNullOrWhiteSpace(pieceMark))
                {
                    continue;
                }

                string key = Normalize(pieceMark);
                List<DrawingSnapshot> snapshots;
                if (!refreshed.TryGetValue(key, out snapshots))
                {
                    snapshots = new List<DrawingSnapshot>();
                    refreshed[key] = snapshots;
                }

                snapshots.Add(CreateSnapshot(drawing, key));
            }

            _drawingsByPieceMark = refreshed;
        }

        private static DrawingSnapshot CreateSnapshot(
            SinglePartDrawing drawing,
            string pieceMark)
        {
            var scales = new List<double>();
            try
            {
                var sheet = drawing.GetSheet();
                if (sheet != null)
                {
                    var views = sheet.GetAllViews();
                    while (views.MoveNext())
                    {
                        var view = views.Current as View;
                        if (view != null && view.Attributes != null)
                        {
                            scales.Add(view.Attributes.Scale);
                        }
                    }
                }
            }
            catch
            {
                // Drawing state remains useful even when Tekla cannot expose view data.
            }

            return new DrawingSnapshot
            {
                PartIdentifier = drawing.PartIdentifier.ID,
                PieceMark = pieceMark,
                Name = drawing.Name,
                Mark = drawing.Mark,
                CreationDate = drawing.CreationDate,
                ModificationDate = drawing.ModificationDate,
                UpToDateStatus = drawing.UpToDateStatus.ToString(),
                IsLocked = drawing.IsLocked,
                IsFrozen = drawing.IsFrozen,
                IsIssued = drawing.IsIssued,
                IsIssuedButModified = drawing.IsIssuedButModified,
                IsReadyForIssue = drawing.IsReadyForIssue,
                ViewScales = scales
            };
        }

        private static string Normalize(string pieceMark)
        {
            return (pieceMark ?? string.Empty).Trim();
        }
    }
}
