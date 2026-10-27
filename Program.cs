using System.Globalization;
using Tracker.Domain;
using Tracker.Input;
using Tracker.Application;
using Tracker.Native;

namespace Tracker;

internal static class Program
{
    private static readonly DateOnly SampleDay = new(2026, 10, 13);

    private static int Main(string[] args)
    {
        Result<string> outcome = args switch
        {
            ["--reports", .. var rest] => Reports(rest),
            ["--native-failure-demo", .. var rest] => Reports(rest, new NativeLoadAdapter((_, _) => throw new DllNotFoundException("Controlled demonstration failure."))),
            ["--error-demo"] or ["--workflow-demo"] => Demonstrate(),
            ["--lookup", .. var rest] => Lookup(rest),
            ["--task", .. var rest] => Request(rest),
            _ => Daily(args)
        };
        // Expected failures are handled once, after the chain has finished.
        switch (outcome)
        {
            case Result<string>.Success success:
                Console.WriteLine(success.Value);
                return 0;
            case Result<string>.Failure failure:
                Console.Error.WriteLine(failure.Error);
                return 1;
            default:
                throw new InvalidOperationException("Unknown application outcome.");
        }
    }

    private static Result<string> Reports(string[] args, NativeLoadAdapter? native = null)
    {
        if (args.Length > 5)
            return Result<string>.Fail("Usage: DailyPlanner --reports [file] [export.csv] [available minutes] [yyyy-MM-dd] [minimum importance]");
        string input = args.ElementAtOrDefault(0) ?? "samples/tasks.json";
        string export = args.ElementAtOrDefault(1) ?? "output/planner-report.csv";
        if (!int.TryParse(args.ElementAtOrDefault(2) ?? "180", out int capacity))
            return Result<string>.Fail("Report available minutes must be an integer.");
        if (!ParseDate(args.ElementAtOrDefault(3) ?? "2026-10-13", out var day))
            return Result<string>.Fail("Report date must be a real date in yyyy-MM-dd format.");
        if (!int.TryParse(args.ElementAtOrDefault(4) ?? "3", out int minimum))
            return Result<string>.Fail("Report minimum importance must be an integer.");
        return ReportWorkflow.Run(input, export, day, minimum, capacity, native).Map(ReportPresentation.Describe);
    }

    private static Result<string> Daily(string[] args)
    {
        if (args.Length > 5)
            return Result<string>.Fail("Usage: DailyPlanner [file] [yyyy-MM-dd] [both|importance|short] [minimum importance] [available minutes]");
        string path = args.ElementAtOrDefault(0) ?? "samples/tasks.json";
        string dateText = args.ElementAtOrDefault(1) ?? "2026-10-13";
        string policyText = args.ElementAtOrDefault(2) ?? "both";
        string minimumText = args.ElementAtOrDefault(3) ?? "3";
        string capacityText = args.ElementAtOrDefault(4) ?? "180";
        if (!ParseDate(dateText, out var date))
            return Result<string>.Fail("Date must be a real date in yyyy-MM-dd format.");
        if (!int.TryParse(minimumText, out int minimum) || minimum is < 1 or > 5)
            return Result<string>.Fail("Minimum importance must be an integer from 1 to 5.");
        if (!int.TryParse(capacityText, out int available))
            return Result<string>.Fail("Available minutes must be an integer.");
        return Policies(policyText).Bind(policies => TaskFile.Load(path).Bind(tasks =>
        {
            // No output or external update is made if a policy fails.
            Result<string> output = Result<string>.Ok("");
            foreach (var policy in policies)
            {
                output = output.Bind(previous => Result<DailyPlan>.Ok(
                        TaskPipeline.CreatePlan(tasks, date, minimum, policy.Score))
                    .Bind(plan => TaskRequestWorkflow.PrepareAndConfirm(PlanningState.Start(plan, available))
                        .Map(confirmed => previous + DescribePlan(policy.Name, plan, confirmed) + "\n")));
            }
            return output.Map(text => text.TrimEnd());
        }));
    }

    private static Result<string> Request(string[] args)
    {
        if (args.Length is < 1 or > 5)
            return Result<string>.Fail("Usage: DailyPlanner --task ID [file] [yyyy-MM-dd] [importance|short] [available minutes]");
        string path = args.ElementAtOrDefault(1) ?? "samples/tasks.json";
        if (!ParseDate(args.ElementAtOrDefault(2) ?? "2026-10-13", out var date))
            return Result<string>.Fail("Date must be a real date in yyyy-MM-dd format.");
        if (!int.TryParse(args.ElementAtOrDefault(4) ?? "180", out int available))
            return Result<string>.Fail("Available minutes must be an integer.");
        return Policies(args.ElementAtOrDefault(3) ?? "importance").Bind(policies =>
            policies.Count != 1
                ? Result<string>.Fail("A task request needs one policy: importance or short.")
                : TaskFile.Load(path)
                    .Bind(tasks => TaskRequestWorkflow.Request(tasks, args[0], date, available, policies[0].Score))
                    .Map(confirmed => DescribePlan(policies[0].Name, confirmed.Session.Plan, confirmed)));
    }

    private static Result<string> Lookup(string[] args)
    {
        if (args.Length is < 1 or > 2)
            return Result<string>.Fail("Usage: DailyPlanner --lookup ID [file]");
        return TaskFile.Load(args.ElementAtOrDefault(1) ?? "samples/tasks.json")
            .Map(tasks => DescribeLookup(args[0], TaskLookup.Find(tasks, args[0])));
    }

    private static string DescribeLookup(string id, Option<TaskItem> result) => result switch
    {
        Option<TaskItem>.Some found => $"Found {found.Value.Id}: {found.Value.Title}; minutes={found.Value.Minutes}; completed={found.Value.Completed.ToString().ToLowerInvariant()}",
        Option<TaskItem>.None => $"No task with ID '{id}'. Lookup is optional; no plan was requested.",
        _ => throw new InvalidOperationException("Unknown lookup outcome.")
    };

    private static Result<string> Demonstrate() => TaskFile.Load("samples/tasks.json").Map(tasks =>
    {
        var plan = TaskPipeline.CreatePlan(tasks, SampleDay, 3, ScoringPolicies.ImportanceFirst);
        var draft = PlanningState.Start(plan, 180);
        var lines = new List<string>
        {
            DescribeLookup("T1", TaskLookup.Find(tasks, "T1")),
            DescribeLookup("UNKNOWN", TaskLookup.Find(tasks, "UNKNOWN")),
            "Normal request: " + DescribeOutcome(TaskRequestWorkflow.Request(tasks, "T1", SampleDay, 180, ScoringPolicies.ImportanceFirst)),
            "Missing required task: " + DescribeOutcome(TaskRequestWorkflow.Request(tasks, "UNKNOWN", SampleDay, 180, ScoringPolicies.ImportanceFirst)),
            "Completed task: " + DescribeOutcome(TaskRequestWorkflow.Request(tasks, "T5", SampleDay, 180, ScoringPolicies.ImportanceFirst)),
            "Wrong date: " + DescribeOutcome(TaskRequestWorkflow.Request(tasks, "T6", SampleDay, 180, ScoringPolicies.ImportanceFirst)),
            "Insufficient capacity: " + DescribeOutcome(TaskRequestWorkflow.Request(tasks, "T1", SampleDay, 59, ScoringPolicies.ImportanceFirst)),
            "Missing prerequisite: " + PlanningPresentation.Describe(PlanningWorkflow.Confirm(draft)),
            "Factory failure: " + PlanningPresentation.Describe(ValidatedPlanningSession.Create(plan, 119)),
            "Rejected preparation: " + PlanningPresentation.Describe(PlanningWorkflow.Prepare(PlanningState.Start(plan, 119)))
        };
        if (TaskRequestWorkflow.PrepareAndConfirm(draft) is Result<PlanningState.Confirmed>.Success good)
        {
            lines.Add(PlanningPresentation.Describe(good.Value));
            lines.Add("Repeated operation: " + PlanningPresentation.Describe(PlanningWorkflow.Confirm(good.Value)));
            lines.Add("Still confirmed: " + PlanningPresentation.Describe(good.Value));
        }
        return string.Join(Environment.NewLine, lines);
    });

    private static string DescribeOutcome(Result<PlanningState.Confirmed> result) => result switch
    {
        Result<PlanningState.Confirmed>.Success success => PlanningPresentation.Describe(success.Value),
        Result<PlanningState.Confirmed>.Failure failure => "Rejected: " + failure.Error,
        _ => throw new InvalidOperationException("Unknown request outcome.")
    };

    private static string DescribePlan(string policy, DailyPlan plan, PlanningState.Confirmed confirmed)
    {
        var lines = new List<string> { $"{policy} | {plan.Date:yyyy-MM-dd}" };
        lines.AddRange(plan.Tasks.Select(item => $"{item.Position}. {item.Task.Id} | {item.Task.Title} | score {item.Score} | {item.Task.Minutes} min"));
        lines.Add($"Selected: {plan.Tasks.Count}; total: {plan.TotalMinutes} min");
        lines.Add($"{plan.Date:yyyy-MM-dd}: ready to confirm; {confirmed.Session.RemainingMinutes} minutes left");
        lines.Add(PlanningPresentation.Describe(confirmed));
        return string.Join(Environment.NewLine, lines);
    }

    private static bool ParseDate(string text, out DateOnly date) => DateOnly.TryParseExact(
        text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private sealed record Policy(string Name, Func<TaskItem, int> Score);

    private static Result<IReadOnlyList<Policy>> Policies(string name) => name switch
    {
        "importance" => Result<IReadOnlyList<Policy>>.Ok([new("Importance first", ScoringPolicies.ImportanceFirst)]),
        "short" => Result<IReadOnlyList<Policy>>.Ok([new("Short tasks first", ScoringPolicies.ShortTasksFirst)]),
        "both" => Result<IReadOnlyList<Policy>>.Ok([new("Importance first", ScoringPolicies.ImportanceFirst), new("Short tasks first", ScoringPolicies.ShortTasksFirst)]),
        _ => Result<IReadOnlyList<Policy>>.Fail("Policy must be both, importance, or short.")
    };
}
