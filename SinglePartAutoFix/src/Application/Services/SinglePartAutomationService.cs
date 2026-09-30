using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Logging;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SinglePartAutoFix.Application.Services
{
    public sealed class SinglePartAutomationService
    {
        private readonly ITeklaModelSession _session;
        private readonly IPartReader _partReader;
        private readonly IDrawingProcessor _drawingProcessor;
        private readonly IProcessLogger _logger;

        public SinglePartAutomationService(
            ITeklaModelSession session,
            IPartReader partReader,
            IDrawingProcessor drawingProcessor,
            IProcessLogger logger)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _partReader = partReader ?? throw new ArgumentNullException(nameof(partReader));
            _drawingProcessor = drawingProcessor ?? throw new ArgumentNullException(nameof(drawingProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ConnectionInfo GetConnectionInfo()
        {
            bool connected = _session.IsConnected();
            return new ConnectionInfo
            {
                IsConnected = connected,
                ModelName = connected ? _session.GetModelName() : string.Empty,
                ModelPath = connected ? _session.GetModelPath() : string.Empty
            };
        }

        public PreparationResult PrepareCandidates(PartQuery query)
        {
            var parts = _partReader.GetParts(query);
            var usableParts = parts.Where(part => !string.IsNullOrWhiteSpace(part.PieceMark)).ToList();
            var candidates = usableParts
                .GroupBy(part => part.PieceMark, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var representative = group.First();
                    return new DrawingCandidate
                    {
                        RepresentativePartId = representative.Id,
                        PieceMark = representative.PieceMark,
                        Profile = representative.Profile,
                        Material = representative.Material,
                        MaterialType = representative.MaterialType,
                        PartCount = group.Count(),
                        IsNumberingUpToDate = group.All(part => part.isNumberingUpToDate)
                    };
                })
                .ToList();

            return new PreparationResult
            {
                SelectedPartCount = parts.Count,
                ExcludedPartCount = parts.Count - usableParts.Count,
                Candidates = candidates
            };
        }

        public IReadOnlyList<DrawingProcessResult> RunDryRun(IReadOnlyList<DrawingCandidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            return CompleteDryRun(candidates.Select(ProcessDryRunCandidate).ToList());
        }

        public DrawingProcessResult ProcessDryRunCandidate(DrawingCandidate candidate)
        {
            return _drawingProcessor.Process(candidate, true);
        }

        public IReadOnlyList<DrawingProcessResult> CompleteDryRun(IReadOnlyList<DrawingProcessResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            _logger.Write("DRY RUN", results);
            return results;
        }

        public BatchPlan BuildBatchPlan(
            IReadOnlyList<DrawingProcessResult> dryRunResults,
            IEnumerable<string> selectedPieceMarks,
            int batchLimit)
        {
            if (dryRunResults == null) throw new ArgumentNullException(nameof(dryRunResults));
            if (selectedPieceMarks == null) throw new ArgumentNullException(nameof(selectedPieceMarks));
            if (batchLimit <= 0) throw new ArgumentOutOfRangeException(nameof(batchLimit), "Batch limit must be greater than zero.");

            var selected = new HashSet<string>(selectedPieceMarks.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
            var eligible = dryRunResults
                .Where(result => result.Status == DrawingProcessStatus.ReadyToCreate && selected.Contains(result.Candidate.PieceMark))
                .Select(result => result.Candidate)
                .ToList();

            return new BatchPlan
            {
                SelectedReadyCount = eligible.Count,
                BatchLimit = batchLimit,
                Candidates = eligible.Take(batchLimit).ToList()
            };
        }

        public DrawingProcessResult ProcessCandidate(DrawingCandidate candidate)
        {
            return _drawingProcessor.Process(candidate, false);
        }

        public BatchSummary CompleteBatch(IReadOnlyList<DrawingProcessResult> results, TimeSpan duration)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            _logger.Write("CONTROLLED BATCH", results);
            return new BatchSummary { Results = results, Duration = duration };
        }

        public BatchSummary RunBatch(BatchPlan plan, Action<int, int, DrawingCandidate, DrawingProcessResult> progress = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var stopwatch = Stopwatch.StartNew();
            var results = new List<DrawingProcessResult>();
            for (int index = 0; index < plan.Candidates.Count; index++)
            {
                var candidate = plan.Candidates[index];
                var result = ProcessCandidate(candidate);
                results.Add(result);
                progress?.Invoke(index + 1, plan.Candidates.Count, candidate, result);
            }
            stopwatch.Stop();
            return CompleteBatch(results, stopwatch.Elapsed);
        }
    }
}
