using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

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

            var drawingProcessor = new DrawingProcessor(
                drawingChecker,
                drawingCreator
            );

            RunDryRun(drawingProcessor, drawingCandidates);
            RunSingleCreateTest(drawingProcessor, drawingCandidates, "BP/275");
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
                    PartCount = group.Count(),
                    IsNumberingUpToDate = group.All(part => part.isNumberingUpToDate)
                };
            })
            .ToList();
    }

    private static void RunDryRun(
        DrawingProcessor drawingProcessor,
        IReadOnlyList<DrawingCandidate> drawingCandidates)
    {
        Console.WriteLine();
        Console.WriteLine("=== DRAWING PROCESSOR DRY RUN ===");

        var results = drawingCandidates.Select(candidate =>
        drawingProcessor.Process(candidate, dryRun: true)).ToList();

        foreach (var candidate in drawingCandidates)
        {
            var result = drawingProcessor.Process(
                candidate,
                dryRun: true
            );

            Console.WriteLine(
                $"{candidate.PieceMark} | " +
                $"{candidate.Profile} | " +
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
}