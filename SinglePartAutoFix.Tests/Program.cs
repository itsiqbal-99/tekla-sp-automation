using SinglePartAutoFix.Application.Interfaces;
using SinglePartAutoFix.Application.Models;
using SinglePartAutoFix.Application.Services;
using SinglePartAutoFix.Domain.Models;
using SinglePartAutoFix.Infrastructure.Tekla;
using SinglePartAutoFix.src.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SinglePartAutoFix.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run("Grouping uses case-insensitive PART_POS and excludes blanks", TestGrouping);
            Run("Drawing processor preserves validation and dry-run rules", TestProcessorRules);
            Run("Batch plan accepts checked Ready candidates and enforces limit", TestBatchPlan);
            Run("Batch continues after failure and reports summary", TestContinueOnFailure);
            Run("Successful creation updates duplicate cache", TestCacheUpdate);
            Console.WriteLine($"Passed: {_passed}  Failed: {_failed}");
            return _failed == 0 ? 0 : 1;
        }

        private static void TestGrouping()
        {
            var parts = new[]
            {
                Part(1, "B1", true), Part(2, "b1", false), Part(3, "", true), Part(4, "C1", true)
            };
            var service = Service(new FakePartReader(parts), new FakeProcessor());
            var result = service.PrepareCandidates(new PartQuery { SelectionMode = PartSelectionMode.Selected });
            Equal(4, result.SelectedPartCount); Equal(1, result.ExcludedPartCount); Equal(2, result.Candidates.Count);
            var b1 = result.Candidates.First(item => string.Equals(item.PieceMark, "B1", StringComparison.OrdinalIgnoreCase));
            Equal(2, b1.PartCount); False(b1.IsNumberingUpToDate); Equal(1, b1.RepresentativePartId);
        }

        private static void TestProcessorRules()
        {
            var checker = new FakeChecker();
            var creator = new FakeCreator(true);
            var processor = new DrawingProcessor(checker, creator);
            Equal(DrawingProcessStatus.NeedReview, processor.Process(Candidate("N1", false, "STEEL"), true).Status);
            Equal(DrawingProcessStatus.NeedReview, processor.Process(Candidate("C1", true, "CONCRETE"), true).Status);
            checker.Existing.Add("E1");
            Equal(DrawingProcessStatus.Existing, processor.Process(Candidate("E1", true, "STEEL"), true).Status);
            Equal(DrawingProcessStatus.ReadyToCreate, processor.Process(Candidate("R1", true, "STEEL"), true).Status);
            Equal(0, creator.CreateCalls);
        }

        private static void TestBatchPlan()
        {
            var service = Service(new FakePartReader(new PartInfo[0]), new FakeProcessor());
            var results = new[]
            {
                Result("A", DrawingProcessStatus.ReadyToCreate), Result("B", DrawingProcessStatus.Existing),
                Result("C", DrawingProcessStatus.ReadyToCreate), Result("D", DrawingProcessStatus.ReadyToCreate)
            };
            var plan = service.BuildBatchPlan(results, new[] { "a", "B", "C", "D" }, 2);
            Equal(3, plan.SelectedReadyCount); Equal(2, plan.Candidates.Count); Equal("A", plan.Candidates[0].PieceMark); Equal("C", plan.Candidates[1].PieceMark);
            Throws<ArgumentOutOfRangeException>(() => service.BuildBatchPlan(results, new[] { "A" }, 0));
        }

        private static void TestContinueOnFailure()
        {
            var processor = new FakeProcessor(candidate => candidate.PieceMark == "A" ? DrawingProcessStatus.Failed : DrawingProcessStatus.Created);
            var service = Service(new FakePartReader(new PartInfo[0]), processor);
            var plan = new BatchPlan { BatchLimit = 2, SelectedReadyCount = 2, Candidates = new[] { Candidate("A"), Candidate("B") } };
            var summary = service.RunBatch(plan);
            Equal(2, processor.Calls); Equal(1, summary.Failed); Equal(1, summary.Created); Equal(2, summary.Requested);
        }

        private static void TestCacheUpdate()
        {
            var checker = new FakeChecker();
            var processor = new DrawingProcessor(checker, new FakeCreator(true));
            Equal(DrawingProcessStatus.Created, processor.Process(Candidate("X1"), false).Status);
            True(checker.Existing.Contains("X1"));
            Equal(DrawingProcessStatus.Existing, processor.Process(Candidate("X1"), false).Status);
        }

        private static SinglePartAutomationService Service(IPartReader reader, IDrawingProcessor processor) => new SinglePartAutomationService(new FakeSession(), reader, processor, new FakeLogger());
        private static PartInfo Part(int id, string mark, bool numbered) => new PartInfo { Id = id, PieceMark = mark, Profile = "P", Material = "S355", MaterialType = "STEEL", isNumberingUpToDate = numbered };
        private static DrawingCandidate Candidate(string mark, bool numbered = true, string materialType = "STEEL") => new DrawingCandidate { RepresentativePartId = 1, PieceMark = mark, Profile = "P", Material = "S355", MaterialType = materialType, PartCount = 1, IsNumberingUpToDate = numbered };
        private static DrawingProcessResult Result(string mark, DrawingProcessStatus status) => new DrawingProcessResult { Candidate = Candidate(mark), Status = status, Message = status.ToString() };
        private static void Run(string name, Action test) { try { test(); _passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
        private static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
        private static void True(bool value) { if (!value) throw new Exception("Expected true"); }
        private static void False(bool value) { if (value) throw new Exception("Expected false"); }
        private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

        private sealed class FakeSession : ITeklaModelSession { public bool IsConnected() => true; public string GetModelName() => "Test"; public string GetModelPath() => "Test"; }
        private sealed class FakePartReader : IPartReader { private readonly IReadOnlyList<PartInfo> _parts; public FakePartReader(IReadOnlyList<PartInfo> parts) { _parts = parts; } public IReadOnlyList<PartInfo> GetParts(PartQuery query) => _parts; }
        private sealed class FakeLogger : IProcessLogger { public void Write(string processName, IReadOnlyList<DrawingProcessResult> results) { } }
        private sealed class FakeChecker : IDrawingChecker
        {
            public HashSet<string> Existing { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public bool Exists(DrawingCandidate candidate) => Existing.Contains(candidate.PieceMark);
            public void MarkAsExisting(string pieceMark) => Existing.Add(pieceMark);
        }
        private sealed class FakeCreator : IDrawingCreator { private readonly bool _result; public int CreateCalls { get; private set; } public FakeCreator(bool result) { _result = result; } public bool Create(DrawingCandidate candidate) { CreateCalls++; return _result; } }
        private sealed class FakeProcessor : IDrawingProcessor
        {
            private readonly Func<DrawingCandidate, DrawingProcessStatus> _status;
            public int Calls { get; private set; }
            public FakeProcessor(Func<DrawingCandidate, DrawingProcessStatus> status = null) { _status = status ?? (_ => DrawingProcessStatus.ReadyToCreate); }
            public DrawingProcessResult Process(DrawingCandidate candidate, bool dryRun) { Calls++; var status = _status(candidate); return new DrawingProcessResult { Candidate = candidate, Status = status, Message = status.ToString() }; }
        }
    }
}
