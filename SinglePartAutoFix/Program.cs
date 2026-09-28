using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.Linq;
using Tekla.Structures.Model.Operations;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== TEKLA 2026 PART READER TEST ===\n");

        try
        {
            var tekla = new TeklaModelSession();

            if (!tekla.IsConnected())
            {
                Console.WriteLine("FAILED: No connection.");
                return;
            }

            Console.WriteLine("CONNECTED!");
            Console.WriteLine("Model: " + tekla.GetModelName());

            var reader = new TeklaPartReader(tekla);

            Console.WriteLine();
            Console.WriteLine("Select parts in Tekla.");
            Console.WriteLine("After selecting, return here and press ENTER...");
            Console.ReadLine();

            var query = new PartQuery
            {
                SelectionMode = PartSelectionMode.Selected
            };

            var parts = reader.GetParts(query);

            var drawingCandidates = parts.Where(x =>
            x.isNumberingUpToDate && !string.IsNullOrWhiteSpace(x.PieceMark))
                .GroupBy(x => x.PieceMark)
                .Select(group =>
                {
                    var representative = group.First();

                    return new DrawingCandidate
                    {
                        RepresentativePartId = representative.Id,
                        PieceMark = representative.PieceMark,
                        Profile = representative.Profile,
                        Material = representative.Material,
                        PartCount = group.Count()
                    };
                })
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"Selected physical parts : {parts.Count}");

            Console.WriteLine($"Drawing candidates      : {drawingCandidates.Count}");

            Console.WriteLine();
            Console.WriteLine("Drawing candidates:");

            foreach (var candidate in drawingCandidates)
            {
                Console.WriteLine(
                    $"{candidate.PieceMark} | " +
                    $"{candidate.Profile} | " +
                    $"{candidate.Material} | " +
                    $"Qty: {candidate.PartCount} | " +
                    $"Representative ID: {candidate.RepresentativePartId}"
                );
            }

            var drawingChecker = new TeklaDrawingChecker(tekla);
            Console.WriteLine();
            Console.WriteLine(
                "=== EXISTING SINGLE PART DRAWINGS ==="
            );

            drawingChecker.PrintSinglePartDrawings();

            Console.WriteLine();
            Console.WriteLine("=== DRAWING CANDIDATE STATUS ===");

            foreach (var candidate in drawingCandidates)
            {
                bool exists =
                    drawingChecker.Exists(candidate);

                string status =
                    exists
                        ? "EXISTING"
                        : "READY TO CREATE";

                Console.WriteLine(
                    $"{candidate.PieceMark} | " +
                    $"{candidate.Profile} | " +
                    $"{candidate.Material} | " +
                    $"Qty: {candidate.PartCount} | " +
                    $"{status}"
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("\nERROR:");
            Console.WriteLine(ex);
        }

        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine();
    }
}