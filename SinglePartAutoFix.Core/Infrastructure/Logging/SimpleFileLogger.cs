using SinglePartAutoFix.Application.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SinglePartAutoFix.Infrastructure.Logging
{
    public class SimpleFileLogger
    {
        private readonly string _logDirectory;

        public SimpleFileLogger()
        {
            _logDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logs"
            );
        }

        public void Write(
            string processName,
            IReadOnlyList<DrawingProcessResult> results,
            string modelName = null)
        {
            if (results == null || results.Count == 0)
                return;

            Directory.CreateDirectory(_logDirectory);

            string filePath = Path.Combine(
                _logDirectory,
                $"single_part_{DateTime.Now:yyyyMMdd}.log"
            );

            using (var writer = new StreamWriter(
                filePath,
                append: true))
            {
                writer.WriteLine();
                writer.WriteLine(
                    $"=== {processName} | {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==="
                );
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    writer.WriteLine($"Model: {modelName}");
                }

                foreach (var result in results)
                {
                    var candidate = result.Candidate;

                    writer.WriteLine(
                        $"Operation: {result.OperationId ?? "-"} | " +
                        $"{candidate?.PieceMarkDisplay ?? "Unknown candidate"} | " +
                        $"{candidate?.Profile ?? "-"} | " +
                        $"{candidate?.MaterialType ?? "-"} | " +
                        $"Qty: {candidate?.PartCount ?? 0} | " +
                        $"{result.Status} | " +
                        $"{result.StandardizationDisplay} | " +
                        $"{result.DrawingStateDisplay} | " +
                        $"Tekla state enum: {result.DrawingLookup?.Drawing?.UpToDateStatus ?? "-"} | " +
                        $"Duration: {result.DurationMilliseconds} ms | " +
                        $"{result.Message}"
                    );

                    if (!string.IsNullOrWhiteSpace(result.TechnicalError))
                    {
                        writer.WriteLine($"Technical error [{result.OperationId}]: {result.TechnicalError}");
                    }
                }

                writer.WriteLine();
                writer.WriteLine("--- SUMMARY ---");

                writer.WriteLine(
                    $"Total         : {results.Count}"
                );

                writer.WriteLine(
                    $"ReadyToCreate : {results.Count(x => x.Status == DrawingProcessStatus.ReadyToCreate)}"
                );

                writer.WriteLine(
                    $"Existing      : {results.Count(x => x.Status == DrawingProcessStatus.Existing)}"
                );

                writer.WriteLine(
                    $"NeedReview    : {results.Count(x => x.Status == DrawingProcessStatus.NeedReview)}"
                );

                writer.WriteLine(
                    $"Created       : {results.Count(x => x.Status == DrawingProcessStatus.Created)}"
                );

                writer.WriteLine(
                    $"Failed        : {results.Count(x => x.Status == DrawingProcessStatus.Failed)}"
                );

                writer.WriteLine(
                    $"Cancelled     : {results.Count(x => x.Status == DrawingProcessStatus.Cancelled)}"
                );
            }
        }

        public void WriteError(
            string processName,
            string modelName,
            string operationId,
            Exception exception)
        {
            Directory.CreateDirectory(_logDirectory);
            string filePath = Path.Combine(
                _logDirectory,
                $"single_part_{DateTime.Now:yyyyMMdd}.log");

            using (var writer = new StreamWriter(filePath, append: true))
            {
                writer.WriteLine();
                writer.WriteLine(
                    $"=== ERROR {processName} | {DateTime.Now:yyyy-MM-dd HH:mm:ss} | " +
                    $"Operation: {operationId ?? "-"} ===");
                writer.WriteLine($"Model: {modelName ?? "Unknown"}");
                writer.WriteLine(exception?.ToString() ?? "No exception details were provided.");
            }
        }
    }
}
