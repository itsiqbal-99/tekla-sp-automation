using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Runtime.Remoting.Messaging;

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

                if(string.Equals(candidate.MaterialType, "CONCRETE", StringComparison.OrdinalIgnoreCase))
                {
                    return new DrawingProcessResult
                    {
                        Candidate = candidate,
                        Status = DrawingProcessStatus.NeedReview,
                        Message = "Concrete is not supported by the current single part drawing workflow"
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
                    _drawingChecker.MarkAsExisting(candidate.PieceMark);
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
                    Message = $"Drawing creation failed for {candidate.PieceMark}.",
                    TechnicalDetails = ex.ToString()
                };
            }
                
            

        }
    }
}
