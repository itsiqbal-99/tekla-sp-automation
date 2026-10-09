using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Diagnostics;
using System.Linq;

namespace SinglePartAutoFix.Application.Services
{
    public class DrawingProcessor
    {
        private readonly IDrawingChecker _drawingChecker;
        private readonly IDrawingCreator _drawingCreator;
        private readonly DrawingStandardProfile _standardProfile;

        public DrawingProcessor(
            IDrawingChecker drawingChecker,
            IDrawingCreator drawingCreator,
            DrawingStandardProfile standardProfile)
        {
            _drawingChecker = drawingChecker ?? throw new ArgumentNullException(nameof(drawingChecker));
            _drawingCreator = drawingCreator ?? throw new ArgumentNullException(nameof(drawingCreator));
            _standardProfile = standardProfile;
        }

        public DrawingProcessResult Process(DrawingCandidate candidate, bool dryRun)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            DateTime startedAt = DateTime.Now;
            var stopwatch = Stopwatch.StartNew();
            string operationId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            try
            {
                if (candidate.HasValidationIssue)
                {
                    return Complete(
                        candidate,
                        DrawingProcessStatus.NeedReview,
                        candidate.ValidationMessage,
                        "Correct the part properties or numbering in Tekla, then select the parts again.",
                        operationId,
                        startedAt,
                        stopwatch);
                }

                if (!candidate.IsNumberingUpToDate)
                {
                    return Complete(
                        candidate,
                        DrawingProcessStatus.NeedReview,
                        "Part numbering is not up to date.",
                        "Update numbering in Tekla, then run the dry check again.",
                        operationId,
                        startedAt,
                        stopwatch);
                }

                if (string.Equals(candidate.MaterialType, "CONCRETE", StringComparison.OrdinalIgnoreCase))
                {
                    return Complete(
                        candidate,
                        DrawingProcessStatus.NeedReview,
                        "Concrete is not supported by the current Single Part Drawing workflow.",
                        "Create or review this drawing manually in Tekla.",
                        operationId,
                        startedAt,
                        stopwatch);
                }

                DrawingLookupResult lookup = _drawingChecker.Find(candidate);
                DrawingProcessResult lookupResult = BuildLookupResult(
                    candidate,
                    lookup,
                    operationId,
                    startedAt,
                    stopwatch);
                if (lookupResult != null)
                {
                    return lookupResult;
                }

                if (dryRun)
                {
                    return Complete(
                        candidate,
                        DrawingProcessStatus.ReadyToCreate,
                        "Drawing is ready to create.",
                        "Select this row and start a controlled batch.",
                        operationId,
                        startedAt,
                        stopwatch,
                        lookup,
                        DrawingStandardizationResult.PendingCreation());
                }

                // A fresh lookup immediately before Insert closes the stale dry-check window.
                lookup = _drawingChecker.Find(candidate, forceRefresh: true);
                lookupResult = BuildLookupResult(
                    candidate,
                    lookup,
                    operationId,
                    startedAt,
                    stopwatch);
                if (lookupResult != null)
                {
                    return lookupResult;
                }

                bool created = _drawingCreator.Create(candidate, _standardProfile);
                if (!created)
                {
                    return Complete(
                        candidate,
                        DrawingProcessStatus.Failed,
                        "Tekla did not create the drawing.",
                        "Review the candidate in Tekla and retry. Use the operation ID when contacting support.",
                        operationId,
                        startedAt,
                        stopwatch,
                        standardization: DrawingStandardizationResult.Failed("Drawing creation failed."));
                }

                _drawingChecker.Refresh();
                DrawingLookupResult createdLookup = _drawingChecker.Find(candidate);
                DrawingVerificationResult verification = VerifyCreatedDrawing(candidate, createdLookup);
                DrawingStandardizationResult standardization = BuildStandardizationResult(
                    candidate,
                    verification);

                string message = verification.IsVerified
                    ? "Drawing was created and verified."
                    : "Drawing was created, but it needs review.";
                string action = verification.IsVerified
                    ? "Open the drawing in Tekla for Engineering review."
                    : verification.Message;

                return Complete(
                    candidate,
                    DrawingProcessStatus.Created,
                    message,
                    action,
                    operationId,
                    startedAt,
                    stopwatch,
                    createdLookup,
                    standardization,
                    verification);
            }
            catch (Exception ex)
            {
                return Complete(
                    candidate,
                    DrawingProcessStatus.Failed,
                    $"Processing failed. Reference: {operationId}.",
                    "Check the Tekla connection and model, then retry or contact support.",
                    operationId,
                    startedAt,
                    stopwatch,
                    technicalError: ex.ToString());
            }
        }

        private DrawingProcessResult BuildLookupResult(
            DrawingCandidate candidate,
            DrawingLookupResult lookup,
            string operationId,
            DateTime startedAt,
            Stopwatch stopwatch)
        {
            if (lookup == null || lookup.Status == DrawingLookupStatus.NotFound)
            {
                return null;
            }

            if (lookup.Status == DrawingLookupStatus.Duplicate)
            {
                return Complete(
                    candidate,
                    DrawingProcessStatus.NeedReview,
                    lookup.Message,
                    "Resolve duplicate drawings in Tekla before continuing.",
                    operationId,
                    startedAt,
                    stopwatch,
                    lookup,
                    DrawingStandardizationResult.NotApplicable("Duplicate drawings require manual review."));
            }

            if (lookup.Status == DrawingLookupStatus.Failed)
            {
                return Complete(
                    candidate,
                    DrawingProcessStatus.Failed,
                    lookup.Message,
                    "Refresh the Tekla connection and run the dry check again.",
                    operationId,
                    startedAt,
                    stopwatch,
                    lookup);
            }

            return Complete(
                candidate,
                DrawingProcessStatus.Existing,
                "A Single Part Drawing already exists.",
                "Open the drawing in Tekla or review its current state.",
                operationId,
                startedAt,
                stopwatch,
                lookup,
                DrawingStandardizationResult.NotApplicable("Existing drawing was not changed."));
        }

        private DrawingVerificationResult VerifyCreatedDrawing(
            DrawingCandidate candidate,
            DrawingLookupResult lookup)
        {
            if (lookup == null || lookup.Status == DrawingLookupStatus.NotFound)
            {
                return VerificationFailure(
                    "Drawing lookup",
                    "Exactly one drawing",
                    "Not found",
                    "Tekla reported success, but the drawing could not be found after creation.");
            }

            if (lookup.Status == DrawingLookupStatus.Duplicate)
            {
                return VerificationFailure(
                    "Drawing lookup",
                    "Exactly one drawing",
                    lookup.DrawingCount.ToString(),
                    "Multiple drawings were found after creation. Resolve the duplicates in Tekla.");
            }

            if (lookup.Status != DrawingLookupStatus.Found || lookup.Drawing == null)
            {
                return VerificationFailure(
                    "Drawing lookup",
                    "Readable drawing",
                    "Unavailable",
                    "The created drawing could not be read from Tekla.");
            }

            bool identifierMatches = candidate.ModelPartIds != null &&
                                     candidate.ModelPartIds.Contains(lookup.Drawing.PartIdentifier);
            if (!identifierMatches &&
                candidate.RepresentativePartId != lookup.Drawing.PartIdentifier)
            {
                return VerificationFailure(
                    "Drawing part identity",
                    "One of the selected physical part IDs",
                    lookup.Drawing.PartIdentifier.ToString(),
                    "The drawing mark matches, but its Tekla part identifier does not match this candidate.");
            }

            if (lookup.Drawing.ViewCount == 0)
            {
                return VerificationFailure(
                    "Drawing views",
                    "At least one view",
                    "0 views",
                    "The drawing has no readable views. Open it in Tekla and review the applied settings.");
            }

            if (_standardProfile == null || !_standardProfile.ExpectedViewScale.HasValue)
            {
                return new DrawingVerificationResult
                {
                    IsVerified = false,
                    CheckName = "Drawing standard",
                    ExpectedValue = "Configured verification rule",
                    ActualValue = "No expected scale configured",
                    Message = "The settings were requested, but no verification rule is configured."
                };
            }

            double expectedScale = _standardProfile.ExpectedViewScale.Value;
            bool scalesMatch = lookup.Drawing.ViewScales.All(
                scale => Math.Abs(scale - expectedScale) < 0.01);

            if (!scalesMatch)
            {
                return VerificationFailure(
                    "View scale",
                    $"1:{expectedScale:0.##}",
                    lookup.Drawing.ScaleDisplay,
                    "The created drawing uses an unexpected view scale. Review it in Tekla.");
            }

            return new DrawingVerificationResult
            {
                IsVerified = true,
                CheckName = "Post-create verification",
                ExpectedValue = $"Drawing present; views use 1:{expectedScale:0.##}",
                ActualValue = $"{lookup.Drawing.ViewCount} view(s); {lookup.Drawing.ScaleDisplay}",
                Message = "Drawing existence and configured view scale were verified."
            };
        }

        private DrawingStandardizationResult BuildStandardizationResult(
            DrawingCandidate candidate,
            DrawingVerificationResult verification)
        {
            if (verification.IsVerified)
            {
                return DrawingStandardizationResult.Verified(verification.Message);
            }

            if (_standardProfile != null && _standardProfile.IsEnabled)
            {
                if (!_standardProfile.ExpectedViewScale.HasValue &&
                    verification.CheckName == "Drawing standard")
                {
                    return DrawingStandardizationResult.SettingsRequested(verification.Message);
                }

                return DrawingStandardizationResult.NeedReview(verification.Message);
            }

            return DrawingStandardizationResult.NotConfigured();
        }

        private static DrawingVerificationResult VerificationFailure(
            string checkName,
            string expected,
            string actual,
            string message)
        {
            return new DrawingVerificationResult
            {
                IsVerified = false,
                CheckName = checkName,
                ExpectedValue = expected,
                ActualValue = actual,
                Message = message
            };
        }

        private static DrawingProcessResult Complete(
            DrawingCandidate candidate,
            DrawingProcessStatus status,
            string message,
            string suggestedAction,
            string operationId,
            DateTime startedAt,
            Stopwatch stopwatch,
            DrawingLookupResult lookup = null,
            DrawingStandardizationResult standardization = null,
            DrawingVerificationResult verification = null,
            string technicalError = null)
        {
            stopwatch.Stop();
            return new DrawingProcessResult
            {
                Candidate = candidate,
                Status = status,
                Message = message,
                SuggestedAction = suggestedAction,
                OperationId = operationId,
                StartedAt = startedAt,
                CompletedAt = DateTime.Now,
                DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                DrawingLookup = lookup,
                Standardization = standardization,
                Verification = verification,
                TechnicalError = technicalError
            };
        }
    }
}
