using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Infrastructure.Logging;
using SinglePartAutoFix.Infrastructure.Tekla;
using System;
using System.Linq;

namespace SinglePartAutoFix.Cli
{
    internal static class Program
    {
        private const int DefaultBatchLimit = 3;

        private static void Main()
        {
            Console.WriteLine("=== SINGLE PART DRAWING AUTOMATION ===\n");
            try
            {
                var session = new TeklaModelSession();
                var checker = new TeklaDrawingChecker(session);
                var service = new SinglePartAutomationService(
                    session,
                    new TeklaPartReader(session),
                    new DrawingProcessor(checker, new TeklaDrawingCreator(session)),
                    new SimpleFileLogger());

                var connection = service.GetConnectionInfo();
                if (!connection.IsConnected)
                {
                    Console.WriteLine("FAILED: No Tekla model connection.");
                    return;
                }

                Console.WriteLine("CONNECTED!");
                Console.WriteLine($"Model: {connection.ModelName}");
                Console.WriteLine("\nSelect parts in Tekla.");
                Console.WriteLine("After selecting, return here and press ENTER...");
                Console.ReadLine();

                var preparation = service.PrepareCandidates(new PartQuery { SelectionMode = PartSelectionMode.Selected });
                Console.WriteLine($"\nSelected physical parts : {preparation.SelectedPartCount}");
                Console.WriteLine($"Drawing candidates      : {preparation.Candidates.Count}");

                Console.WriteLine("\n=== DRAWING PROCESSOR DRY RUN ===");
                var dryRun = service.RunDryRun(preparation.Candidates);
                PrintResults(dryRun);

                var readyMarks = dryRun
                    .Where(result => result.Status == DrawingProcessStatus.ReadyToCreate)
                    .Select(result => result.Candidate.PieceMark)
                    .ToList();
                var plan = service.BuildBatchPlan(dryRun, readyMarks, DefaultBatchLimit);

                Console.WriteLine("\n=== CONTROLLED BATCH ===");
                if (plan.Candidates.Count == 0)
                {
                    Console.WriteLine("No drawings ready to create.");
                    return;
                }

                Console.WriteLine($"Drawings to Create: {plan.Candidates.Count}");
                foreach (var candidate in plan.Candidates)
                    Console.WriteLine($"{candidate.PieceMark} | {candidate.Profile} | {candidate.MaterialType} | {candidate.PartCount}");

                Console.Write("\nContinue Creating these Drawings? (Y/N) ");
                if (!string.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Batch Cancelled");
                    return;
                }

                var summary = service.RunBatch(plan, (completed, total, candidate, result) =>
                    Console.WriteLine($"{candidate.PieceMark} | {result.Status} | {result.Message}"));
                Console.WriteLine("\n=== BATCH SUMMARY ===");
                Console.WriteLine($"Processed   : {summary.Requested}");
                Console.WriteLine($"Created     : {summary.Created}");
                Console.WriteLine($"Existing    : {summary.Existing}");
                Console.WriteLine($"Need Review : {summary.NeedReview}");
                Console.WriteLine($"Failed      : {summary.Failed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nERROR:");
                Console.WriteLine(ex);
            }
            finally
            {
                Console.WriteLine("\nPress ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static void PrintResults(System.Collections.Generic.IReadOnlyList<DrawingProcessResult> results)
        {
            foreach (var result in results)
            {
                var candidate = result.Candidate;
                Console.WriteLine($"{candidate.PieceMark} | {candidate.Profile} | {candidate.MaterialType} | Qty: {candidate.PartCount} | {result.Status} | {result.Message}");
            }
            Console.WriteLine("\n=== SUMMARY ===");
            foreach (DrawingProcessStatus status in Enum.GetValues(typeof(DrawingProcessStatus)))
                Console.WriteLine($"{status,-14}: {results.Count(result => result.Status == status)}");
        }
    }
}
