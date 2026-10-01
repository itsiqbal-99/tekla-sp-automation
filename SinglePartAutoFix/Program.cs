using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Logging;
using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.src.Application.Services;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== SINGLE PART DRAWING AUTOMATION ===\n");

        try
        {
            var tekla = new TeklaModelSession();

            if (!tekla.IsConnected())
            {
                Console.WriteLine("FAILED: No Tekla model connection.");
                return;
            }

            Console.WriteLine("CONNECTED!");
            Console.WriteLine($"Model: {tekla.GetModelName()}");

            var partReader = new TeklaPartReader(tekla);

            Console.WriteLine();
            Console.WriteLine("Select parts in Tekla.");
            Console.WriteLine("After selecting, return here and press ENTER...");
            Console.ReadLine();

            var query = new PartQuery
            {
                SelectionMode = PartSelectionMode.Selected
            };

            var parts = partReader.GetParts(query);
            var drawingCandidates = BuildDrawingCandidates(parts);


            Console.WriteLine();
            Console.WriteLine($"Selected physical parts : {parts.Count}");
            Console.WriteLine($"Drawing candidates      : {drawingCandidates.Count}");


            var drawingChecker = new TeklaDrawingChecker(tekla);
            var drawingCreator = new TeklaDrawingCreator(tekla);
            var drawingStandardizer = new NoOpDrawingStandardizer();

            var drawingProcessor = new DrawingProcessor(
                drawingChecker,
                drawingCreator,
                drawingStandardizer
            );

            var logger = new SimpleFileLogger();

            var dryRunResult = RunDryRun(drawingProcessor, drawingCandidates);
            logger.Write("DRY RUN", dryRunResult);

            var totalReady = dryRunResult.Count(x => x.Status == DrawingProcessStatus.ReadyToCreate);

            

            var batchResults = RunControllerBatch(drawingProcessor, dryRunResult, MaxItems: 3);
            

            logger.Write("CONTROLLED BATCH", batchResults);
            //RunSingleCreateTest(drawingProcessor, drawingCandidates, "BP/275");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("ERROR:");
            Console.WriteLine(ex);
        }

        Console.WriteLine();
        Console.WriteLine("Press ENTER to exit...");
        Console.ReadLine();
    }

    private static List<DrawingCandidate> BuildDrawingCandidates(
        IReadOnlyList<PartInfo> parts)
    {
        return parts
            .Where(part => !string.IsNullOrWhiteSpace(part.PieceMark))
            .GroupBy(part => part.PieceMark)
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
    }

    private static List<DrawingProcessResult> RunDryRun(
        DrawingProcessor drawingProcessor,
        IReadOnlyList<DrawingCandidate> drawingCandidates)
    {
        Console.WriteLine();
        Console.WriteLine("=== DRAWING PROCESSOR DRY RUN ===");

        var results = drawingCandidates.Select(candidate =>
        {
            return drawingProcessor.Process(candidate, dryRun: true);
        }).ToList();

        foreach (var result in results)
        {
            var candidate = result.Candidate;

            Console.WriteLine(
                $"{candidate.PieceMark} | " +
                $"{candidate.Profile} | " +
                $"{candidate.MaterialType} | " +
                $"Qty: {candidate.PartCount} | " +
                $"{result.Status} | " +
                $"{result.Message}"
            );
        }

        Console.WriteLine();
        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"Ready to Create {results.Count(x => x.Status == DrawingProcessStatus.ReadyToCreate)}");
        Console.WriteLine($"Existing {results.Count(x => x.Status == DrawingProcessStatus.Existing)}");
        Console.WriteLine($"Need Review {results.Count(x => x.Status == DrawingProcessStatus.NeedReview)}");
        Console.WriteLine($"Created {results.Count(x => x.Status == DrawingProcessStatus.Created)}");
        Console.WriteLine($"Failed {results.Count(x => x.Status == DrawingProcessStatus.Failed)}");
        return results;
    }

    public static void RunSingleCreateTest(DrawingProcessor drawingProcessor,
    IReadOnlyList<DrawingCandidate> drawingCandidates,
    string pieceMark)
    {
        var candidate = drawingCandidates.FirstOrDefault(x => x.PieceMark == pieceMark);
        Console.WriteLine();
        Console.WriteLine("=== SINGLE DRAWING CREATE TEST ===");

        if (candidate == null)
        {
            Console.WriteLine($"Candidate {pieceMark} not found.");
            return;
        }

        Console.WriteLine($"Piece Mark: {candidate.PieceMark}");
        Console.WriteLine($"Part ID: {candidate.RepresentativePartId}");
        Console.WriteLine($"Profile: {candidate.Profile}");
        Console.WriteLine($"Qty: {candidate.PartCount}");

        var result = drawingProcessor.Process(candidate, dryRun: false);
        Console.WriteLine($"{result.Status} | {result.Message}");
    }

    private static List<DrawingProcessResult> RunControllerBatch(DrawingProcessor drawingProcessor, IReadOnlyList<DrawingProcessResult> dryRunResults, int MaxItems)
    {
        var candidatesToCreate = dryRunResults.Where(result =>
        result.Status == DrawingProcessStatus.ReadyToCreate
        ).Take(MaxItems)
        .Select(result => result.Candidate)
        .ToList();


        Console.WriteLine();
        Console.WriteLine("=== CONTROLLER BATCH ===");

        if(candidatesToCreate.Count == 0)
        {
            Console.WriteLine("No Drawings ready to create.");
            return new List<DrawingProcessResult>();
        }


        Console.WriteLine($"Drawings to Create: {candidatesToCreate.Count}");


        foreach(var candidate in candidatesToCreate)
        {
            Console.WriteLine(
                $"{candidate.PieceMark} |" +
                $"{candidate.Profile} |" +
                $"{candidate.MaterialType} |" +
                $"{candidate.PartCount} |" 
                );

        }
        Console.WriteLine();
        Console.Write("Continue Creating these Drawings? (Y/N)");

        var input = Console.ReadLine();
        if (!string.Equals(input, "Y", StringComparison.OrdinalIgnoreCase)) {
            Console.WriteLine("Batch Cancelled");
            return new List<DrawingProcessResult>();
        }

        Console.WriteLine();
        Console.WriteLine("=== BATCH RESULT ===");

        var startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

        var batchResults = new List<DrawingProcessResult>();

        foreach (var candidate in candidatesToCreate)
        {
            var result = drawingProcessor.Process(candidate, dryRun: false);
            batchResults.Add(result);

            var Standardization = result.Standardization.Status.ToString();

            Console.WriteLine(
                $"{candidate.PieceMark} | " +
                $"{result.Status} | " +
                $"{result.Message}" +
                $"Standardization : {Standardization}" 
                );
        }

        Console.WriteLine();
        Console.WriteLine("=== BATCH SUMMARY ===");
        Console.WriteLine($"Processed   : {batchResults.Count}");
        Console.WriteLine(
            $"Created     : {batchResults.Count(x => x.Status == DrawingProcessStatus.Created)}"
        );

        Console.WriteLine(
            $"Existing    : {batchResults.Count(x => x.Status == DrawingProcessStatus.Existing)}"
        );

        Console.WriteLine(
            $"Need Review : {batchResults.Count(x => x.Status == DrawingProcessStatus.NeedReview)}"
        );

        Console.WriteLine(
            $"Failed      : {batchResults.Count(x => x.Status == DrawingProcessStatus.Failed)}"
        );

        stopwatch.Stop();
        var finishedAt = DateTimeOffset.Now;
        Console.WriteLine($"Started  : {startedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
          Console.WriteLine($"Finished : {finishedAt:yyyy-MM-dd HH:mm:ss.fff zzz}");
        Console.WriteLine($"Duration : {stopwatch.Elapsed}");
        Console.WriteLine($"Total ms : {stopwatch.Elapsed.TotalMilliseconds:N0} ms");

        return batchResults;
    }
}