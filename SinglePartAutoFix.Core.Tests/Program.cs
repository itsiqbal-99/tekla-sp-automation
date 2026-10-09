using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SinglePartAutoFix.Core.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run("Candidate grouping is case-insensitive", CandidateGroupingIsCaseInsensitive);
            Run("Missing PART_POS becomes NeedReview candidate", MissingPieceMarkIsPreserved);
            Run("Inconsistent candidate metadata is detected", InconsistentMetadataIsDetected);
            Run("Numbering failure becomes NeedReview", NumberingFailureBecomesNeedReview);
            Run("Existing drawing state is mapped", ExistingDrawingStateIsMapped);
            Run("Duplicate drawing becomes NeedReview", DuplicateDrawingBecomesNeedReview);
            Run("Final duplicate check prevents insert", FinalDuplicateCheckPreventsInsert);
            Run("Created drawing is verified by scale", CreatedDrawingIsVerified);
            Run("Unexpected scale requires review", UnexpectedScaleRequiresReview);
            Run("Post-create lookup failure requires review", PostCreateLookupFailureRequiresReview);
            Run("Batch continues after one creation failure", BatchContinuesAfterFailure);
            Run("Batch cancellation marks remaining candidates", BatchCancellationMarksCandidates);

            Console.WriteLine($"Passed: {_passed}; Failed: {_failed}");
            return _failed == 0 ? 0 : 1;
        }

        private static void CandidateGroupingIsCaseInsensitive()
        {
            var candidates = new DrawingCandidateBuilder().Build(new[]
            {
                Part(1, " BP/1 ", "PL10", "STEEL"),
                Part(2, "bp/1", "PL10", "STEEL")
            });

            Equal(1, candidates.Count);
            Equal(2, candidates[0].PartCount);
            Equal(2, candidates[0].ModelPartIds.Count);
            Equal("BP/1", candidates[0].PieceMark);
        }

        private static void MissingPieceMarkIsPreserved()
        {
            var candidates = new DrawingCandidateBuilder().Build(new[]
            {
                Part(7, string.Empty, "PL10", "STEEL")
            });

            Equal(1, candidates.Count);
            True(candidates[0].HasValidationIssue);

            DrawingProcessResult result = Processor(new FakeChecker()).Process(candidates[0], true);
            Equal(DrawingProcessStatus.NeedReview, result.Status);
        }

        private static void InconsistentMetadataIsDetected()
        {
            var candidates = new DrawingCandidateBuilder().Build(new[]
            {
                Part(1, "A/1", "PL10", "STEEL"),
                Part(2, "A/1", "PL12", "STEEL")
            });

            True(candidates.Single().HasValidationIssue);
        }

        private static void DuplicateDrawingBecomesNeedReview()
        {
            var checker = new FakeChecker
            {
                Lookup = new DrawingLookupResult
                {
                    Status = DrawingLookupStatus.Duplicate,
                    DrawingCount = 2,
                    Message = "Duplicate drawings."
                }
            };

            DrawingProcessResult result = Processor(checker).Process(Candidate("A/1"), true);
            Equal(DrawingProcessStatus.NeedReview, result.Status);
        }

        private static void NumberingFailureBecomesNeedReview()
        {
            DrawingCandidate candidate = Candidate("A/4");
            candidate.IsNumberingUpToDate = false;

            DrawingProcessResult result = Processor(new FakeChecker()).Process(candidate, true);
            Equal(DrawingProcessStatus.NeedReview, result.Status);
            True(result.Message.IndexOf("numbering", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void ExistingDrawingStateIsMapped()
        {
            var checker = new FakeChecker
            {
                Lookup = new DrawingLookupResult
                {
                    Status = DrawingLookupStatus.Found,
                    DrawingCount = 1,
                    Drawing = new DrawingSnapshot
                    {
                        PartIdentifier = 1,
                        UpToDateStatus = "PartsWereModified",
                        IsLocked = true,
                        IsIssued = true,
                        ViewScales = new[] { 5.0 }
                    }
                }
            };

            DrawingProcessResult result = Processor(checker).Process(Candidate("A/5"), true);
            Equal(DrawingProcessStatus.Existing, result.Status);
            True(result.DrawingStateDisplay.Contains("Locked"));
            True(result.DrawingStateDisplay.Contains("Issued"));
            True(result.DrawingStateDisplay.Contains("Model changed"));
        }

        private static void FinalDuplicateCheckPreventsInsert()
        {
            var checker = new FakeChecker
            {
                Lookup = DrawingLookupResult.NotFound(),
                ForcedLookup = new DrawingLookupResult
                {
                    Status = DrawingLookupStatus.Duplicate,
                    DrawingCount = 2,
                    Message = "Duplicate drawings."
                }
            };
            var creator = new FakeCreator();

            DrawingProcessResult result = Processor(checker, creator).Process(Candidate("A/6"), false);
            Equal(DrawingProcessStatus.NeedReview, result.Status);
            Equal(0, creator.CallCount);
        }

        private static void CreatedDrawingIsVerified()
        {
            var checker = new FakeChecker { CreateSnapshotAfterRefresh = true };
            DrawingProcessResult result = Processor(checker).Process(Candidate("A/2"), false);

            Equal(DrawingProcessStatus.Created, result.Status);
            Equal(DrawingStandardizationStatus.Verified, result.Standardization.Status);
            True(result.Verification.IsVerified);
        }

        private static void BatchCancellationMarksCandidates()
        {
            var runner = new DrawingBatchRunner(Processor(new FakeChecker()));
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var results = runner.RunAsync(
                new[] { Candidate("A/1"), Candidate("A/2") },
                cancellation.Token,
                null,
                null).GetAwaiter().GetResult();

            Equal(2, results.Count);
            True(results.All(result => result.Status == DrawingProcessStatus.Cancelled));
        }

        private static void UnexpectedScaleRequiresReview()
        {
            var checker = new FakeChecker
            {
                CreateSnapshotAfterRefresh = true,
                CreatedScale = 10.0
            };

            DrawingProcessResult result = Processor(checker).Process(Candidate("A/3"), false);
            Equal(DrawingProcessStatus.Created, result.Status);
            Equal(DrawingStandardizationStatus.NeedReview, result.Standardization.Status);
            True(!result.Verification.IsVerified);
        }

        private static void PostCreateLookupFailureRequiresReview()
        {
            DrawingProcessResult result = Processor(new FakeChecker()).Process(Candidate("A/7"), false);
            Equal(DrawingProcessStatus.Created, result.Status);
            Equal(DrawingStandardizationStatus.NeedReview, result.Standardization.Status);
            True(!result.Verification.IsVerified);
        }

        private static void BatchContinuesAfterFailure()
        {
            var creator = new FakeCreator { FailFirstCall = true };
            var runner = new DrawingBatchRunner(Processor(new FakeChecker(), creator));

            var results = runner.RunAsync(
                new[] { Candidate("A/8"), Candidate("A/9") },
                CancellationToken.None,
                null,
                null).GetAwaiter().GetResult();

            Equal(2, results.Count);
            Equal(DrawingProcessStatus.Failed, results[0].Status);
            Equal(DrawingProcessStatus.Created, results[1].Status);
            Equal(2, creator.CallCount);
        }

        private static DrawingProcessor Processor(FakeChecker checker, FakeCreator creator = null)
        {
            return new DrawingProcessor(
                checker,
                creator ?? new FakeCreator(),
                new DrawingStandardProfile(
                    "test", "Test", "TEST", "1.0", true, "TEST.wd", 5.0));
        }

        private static DrawingCandidate Candidate(string pieceMark)
        {
            return new DrawingCandidate
            {
                RepresentativePartId = 1,
                ModelPartIds = new[] { 1 },
                PieceMark = pieceMark,
                Profile = "PL10",
                Material = "S355",
                MaterialType = "STEEL",
                PartCount = 1,
                IsNumberingUpToDate = true,
                ValidationMessage = string.Empty
            };
        }

        private static PartInfo Part(int id, string mark, string profile, string type)
        {
            return new PartInfo
            {
                Id = id,
                PieceMark = mark,
                Profile = profile,
                Material = "S355",
                MaterialType = type,
                isNumberingUpToDate = true
            };
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("Expected true.");
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
            }
        }

        private sealed class FakeChecker : IDrawingChecker
        {
            public DrawingLookupResult Lookup { get; set; } = DrawingLookupResult.NotFound();
            public DrawingLookupResult ForcedLookup { get; set; }
            public bool CreateSnapshotAfterRefresh { get; set; }
            public double CreatedScale { get; set; } = 5.0;

            public DrawingLookupResult Find(DrawingCandidate candidate, bool forceRefresh = false)
            {
                if (forceRefresh && ForcedLookup != null)
                {
                    return ForcedLookup;
                }

                return Lookup;
            }

            public void Refresh()
            {
                if (!CreateSnapshotAfterRefresh) return;
                Lookup = new DrawingLookupResult
                {
                    Status = DrawingLookupStatus.Found,
                    DrawingCount = 1,
                    Drawing = new DrawingSnapshot
                    {
                        PartIdentifier = 1,
                        PieceMark = "A/2",
                        UpToDateStatus = "DrawingIsUpToDate",
                        ViewScales = new[] { CreatedScale }
                    }
                };
            }
        }

        private sealed class FakeCreator : IDrawingCreator
        {
            public int CallCount { get; private set; }
            public bool FailFirstCall { get; set; }

            public bool Create(DrawingCandidate candidate, DrawingStandardProfile standardProfile = null)
            {
                CallCount++;
                if (FailFirstCall && CallCount == 1)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
