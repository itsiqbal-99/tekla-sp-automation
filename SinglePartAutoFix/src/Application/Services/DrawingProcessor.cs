using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Application.Services
{
    public class DrawingProcessor : IDrawingProcessor
    {
        private readonly IDrawingChecker _drawingChecker;
        private readonly IDrawingCreator _drawingCreator;

        public DrawingProcessor(IDrawingChecker drawingChecked, IDrawingCreator drawingCreator)
        {
            _drawingChecker = drawingChecked;
            _drawingCreator = drawingCreator;
        }

        public DrawingProcessResult Process(DrawingCandidate candidate, bool dryRun)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            try
            {
                if (!candidate.IsNumberingUpToDate)
                {
                    return new DrawingProcessResult
                    {
                        Candidate = candidate,
                        Status = DrawingProcessStatus.NeedReview,
                        Message = "Part Numbering is Not up to date"
                    };
                }

                bool exists = _drawingChecker.Exists(candidate);
                if (exists)
                {
                    return new DrawingProcessResult
                    {
                        Candidate = candidate,
                        Status = DrawingProcessStatus.Existing,
                        Message = "Drawing Already Exists"
                    };
                }
                if (dryRun)
                {
                    return new DrawingProcessResult
                    {
                        Candidate = candidate,
                        Status = DrawingProcessStatus.ReadyToCreate,
                        Message = "Drawing is Ready to Create"
                    };
                }

                bool created = _drawingCreator.Create(candidate);
                if (created)
                {
                    return new DrawingProcessResult
                    {
                        Candidate = candidate,
                        Status = DrawingProcessStatus.Created,
                        Message = "Drawing Created Successfully"
                    };
                }

                return new DrawingProcessResult
                {
                    Candidate = candidate,
                    Status = DrawingProcessStatus.Failed,
                    Message = "Drawing Creation Failed."
                };
            } catch (Exception ex)
            {
                return new DrawingProcessResult
                {
                    Candidate = candidate,
                    Status = DrawingProcessStatus.Failed,
                    Message = $"Processing FAILED: {ex.Message}"
                };
            }
                
            

        }
    }
}
