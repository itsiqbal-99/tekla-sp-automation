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
            IReadOnlyList<DrawingProcessResult> results)
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

                foreach (var result in results)
                {
                    var candidate = result.Candidate;

                    writer.WriteLine(
                        $"{candidate.PieceMark} | " +
                        $"{candidate.Profile} | " +
                        $"{candidate.MaterialType} | " +
                        $"Qty: {candidate.PartCount} | " +
                        $"{result.Status} | " +
                        $"{result.Message}"
                    );
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
            }
        }
    }
}