using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePartAutoFix.Application.Services
{
    public class DrawingBatchRunner
    {
        private readonly DrawingProcessor _processor;

        public DrawingBatchRunner(DrawingProcessor processor)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        }

        public async Task<List<DrawingProcessResult>> RunAsync(
            IReadOnlyList<DrawingCandidate> candidates,
            CancellationToken cancellationToken,
            Action<int, int, string> reportProgress,
            Func<string> validateBeforeItem)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var results = new List<DrawingProcessResult>();
            for (int index = 0; index < candidates.Count; index++)
            {
                DrawingCandidate candidate = candidates[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    AddCancelledResults(results, candidates, index);
                    break;
                }

                string validationMessage = validateBeforeItem == null
                    ? string.Empty
                    : validateBeforeItem();
                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    results.Add(new DrawingProcessResult
                    {
                        Candidate = candidate,
                        Status = DrawingProcessStatus.Failed,
                        Message = validationMessage,
                        SuggestedAction = "Restore the application preflight checks, then run the dry check again.",
                        OperationId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                        StartedAt = DateTime.Now,
                        CompletedAt = DateTime.Now
                    });
                    AddCancelledResults(results, candidates, index + 1);
                    break;
                }

                reportProgress?.Invoke(
                    index,
                    candidates.Count,
                    $"Creating {candidate.PieceMarkDisplay}...");

                // Yield between candidates so WPF can paint progress and accept a cancel request.
                await Task.Yield();
                results.Add(_processor.Process(candidate, dryRun: false));

                reportProgress?.Invoke(
                    index + 1,
                    candidates.Count,
                    $"Completed {index + 1} of {candidates.Count}.");
            }

            return results;
        }

        private static void AddCancelledResults(
            ICollection<DrawingProcessResult> results,
            IReadOnlyList<DrawingCandidate> candidates,
            int startIndex)
        {
            for (int index = startIndex; index < candidates.Count; index++)
            {
                results.Add(new DrawingProcessResult
                {
                    Candidate = candidates[index],
                    Status = DrawingProcessStatus.Cancelled,
                    Message = "Not started because the batch was cancelled.",
                    SuggestedAction = "Run a new dry check when you are ready to continue.",
                    OperationId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                    StartedAt = DateTime.Now,
                    CompletedAt = DateTime.Now
                });
            }
        }
    }
}
