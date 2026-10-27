using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> RequestCases()
    {
        yield return ("Task request chain creates the correct confirmed plan", () =>
        {
            var outcome = TaskRequestWorkflow.Request(Sample(), "T3", Day, 120, ScoringPolicies.ShortTasksFirst);
            if (outcome is not Result<PlanningState.Confirmed>.Success success)
                throw new Exception("Task request did not succeed.");
            Equal("PLAN-20261013", success.Value.Reference);
            Equal(10, success.Value.Session.Plan.TotalMinutes);
            Equal(110, success.Value.Session.RemainingMinutes);
            Equal("T3", success.Value.Session.Plan.Tasks[0].Task.Id);
            Equal(80, success.Value.Session.Plan.Tasks[0].Score);
        });
        yield return ("Missing requested task skips scoring and confirmation", () =>
        {
            int scores = 0, confirmations = 0;
            var outcome = TaskRequestWorkflow.Request(Sample(), "absent", Day, 120,
                task => { scores++; return ScoringPolicies.ImportanceFirst(task); })
                .Map(state => { confirmations++; return state.Reference; });
            RequestFailure(outcome, "absent", "not found");
            Equal(0, scores);
            Equal(0, confirmations);
        });
        yield return ("Completed requested task is a domain failure before scoring", () =>
        {
            int calls = 0;
            var outcome = TaskRequestWorkflow.Request(Sample(), "T5", Day, 120,
                task => { calls++; return ScoringPolicies.ImportanceFirst(task); });
            RequestFailure(outcome, "T5", "already completed");
            Equal(0, calls);
        });
        yield return ("Wrong-day requested task is a domain failure before scoring", () =>
        {
            int calls = 0;
            var outcome = TaskRequestWorkflow.Request(Sample(), "T6", Day, 120,
                task => { calls++; return ScoringPolicies.ImportanceFirst(task); });
            RequestFailure(outcome, "T6", "2026-10-14");
            Equal(0, calls);
        });
        yield return ("Request capacity failures remain Result failures", () =>
        {
            RequestFailure(TaskRequestWorkflow.Request(Sample(), "T1", Day, 59, ScoringPolicies.ImportanceFirst), "Preparation", "exceed");
            RequestFailure(TaskRequestWorkflow.Request(Sample(), "T1", Day, 0, ScoringPolicies.ImportanceFirst), "Available time", "1 and 480");
        });
        yield return ("Request failures preserve caller data and an existing confirmed state", () =>
        {
            var tasks = Sample().ToList();
            var before = tasks.ToArray();
            var confirmed = TaskRequestWorkflow.Request(tasks, "T3", Day, 120, ScoringPolicies.ImportanceFirst);
            if (confirmed is not Result<PlanningState.Confirmed>.Success success)
                throw new Exception("Setup failed.");
            RequestFailure(TaskRequestWorkflow.Request(tasks, "T5", Day, 120, ScoringPolicies.ImportanceFirst), "T5", "completed");
            Sequence(before, tasks);
            Equal(10, success.Value.Session.Plan.TotalMinutes);
            RequestFailure(TaskRequestWorkflow.PrepareAndConfirm(success.Value), "already confirmed", "Preparation");
            Equal("PLAN-20261013", success.Value.Reference);
        });
        yield return ("A present zero-minute reminder can reach confirmation", () =>
        {
            TaskItem[] tasks = [new("REM", "Reminder", Day, 3, 0, false)];
            var outcome = TaskRequestWorkflow.Request(tasks, "REM", Day, 1, ScoringPolicies.ShortTasksFirst);
            Assert(outcome is Result<PlanningState.Confirmed>.Success { Value.Session.Plan.TotalMinutes: 0 }, "Valid reminder rejected.");
        });
    }

    private static void RequestFailure<T>(Result<T> outcome, string first, string second)
    {
        if (outcome is not Result<T>.Failure failure)
            throw new Exception("Expected a Result failure.");
        Assert(failure.Error.Contains(first, StringComparison.OrdinalIgnoreCase), $"Missing context: {failure.Error}");
        Assert(failure.Error.Contains(second, StringComparison.OrdinalIgnoreCase), $"Wrong rule: {failure.Error}");
    }
}
