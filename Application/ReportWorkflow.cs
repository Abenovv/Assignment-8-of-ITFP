using System.Globalization;
using Tracker.Domain;
using Tracker.Input;
using Tracker.Native;

namespace Tracker.Application;

public sealed record WorkloadLoadRow(DateOnly Date, int Tasks, int TotalMinutes, int LoadPercent);
public sealed record ReportBundle(DateOnly Day, IReadOnlyList<FocusRow> Importance,
    IReadOnlyList<FocusRow> ShortTasks, IReadOnlyList<WorkloadLoadRow> Workload);
public sealed record ReportRun(ReportBundle Reports, string ExportPath);

public static class ReportWorkflow
{
    public static Result<ReportRun> Run(string inputPath, string exportPath, DateOnly day,
        int minimumImportance, int availableMinutes, NativeLoadAdapter? native = null)
    {
        if (minimumImportance is < 1 or > 5)
            return Result<ReportRun>.Fail("Report minimum importance must be from 1 to 5.");
        if (availableMinutes is < 1 or > 480)
            return Result<ReportRun>.Fail("Report available minutes must be from 1 to 480.");
        if (day == DateOnly.MaxValue)
            return Result<ReportRun>.Fail("Report date must allow the following day in its workload window.");

        var adapter = native ?? new NativeLoadAdapter();
        return TaskFile.Load(inputPath)
            .Bind(tasks => CreateReports(tasks, day, minimumImportance, availableMinutes, adapter))
            .Bind(reports => ReportExporter.Save(exportPath, ReportPresentation.ToCsv(reports), inputPath)
                .Map(path => new ReportRun(reports, path)));
    }

    private static Result<ReportBundle> CreateReports(IReadOnlyList<TaskItem> tasks, DateOnly day,
        int minimumImportance, int availableMinutes, NativeLoadAdapter native)
    {
        var importance = PlannerReports.DailyFocus(tasks, day, minimumImportance, CurriedScoring.ImportanceFirst);
        var shortTasks = PlannerReports.DailyFocus(tasks, day, minimumImportance, CurriedScoring.ShortTasksFirst);
        var workload = PlannerReports.WorkloadByDay(tasks, day, day.AddDays(1));
        Result<IReadOnlyList<WorkloadLoadRow>> loaded = Result<IReadOnlyList<WorkloadLoadRow>>.Ok(Array.Empty<WorkloadLoadRow>());
        foreach (var row in workload)
        {
            loaded = loaded.Bind(previous => native.Calculate(row.TotalMinutes, availableMinutes)
                .Map(percent => (IReadOnlyList<WorkloadLoadRow>)Array.AsReadOnly(previous
                    .Append(new WorkloadLoadRow(row.Date, row.Tasks, row.TotalMinutes, percent)).ToArray())));
        }
        return loaded.Map(rows => new ReportBundle(day, importance, shortTasks, rows));
    }
}

public static class ReportPresentation
{
    public static string Describe(ReportRun run)
    {
        var rows = new List<string> { $"Daily focus | {run.Reports.Day:yyyy-MM-dd}" };
        foreach (var policy in new[] { ("Importance first", run.Reports.Importance), ("Short tasks first", run.Reports.ShortTasks) })
        {
            rows.Add(policy.Item1);
            rows.AddRange(policy.Item2.Select(row => $"{row.Position}. {row.Id} | {row.Title} | score {row.Score} | {row.Minutes} min"));
            rows.Add($"Tasks: {policy.Item2.Count}; minutes: {policy.Item2.Sum(row => row.Minutes)}");
        }
        rows.Add("Workload by date | tasks | minutes | load percent (native C)");
        rows.AddRange(run.Reports.Workload.Select(row => $"{row.Date:yyyy-MM-dd} | {row.Tasks} | {row.TotalMinutes} | {row.LoadPercent}%"));
        rows.Add("Exported: " + run.ExportPath);
        return string.Join(Environment.NewLine, rows);
    }

    public static string ToCsv(ReportBundle reports)
    {
        var lines = new List<string> { "report,date,policy,position,id,title,tasks,minutes,score,load_percent" };
        foreach (var policy in new[] { ("importance", reports.Importance), ("short", reports.ShortTasks) })
        {
            foreach (var row in policy.Item2)
                lines.Add(string.Join(",", new[] { "focus", reports.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    policy.Item1, Number(row.Position), Csv(row.Id), Csv(row.Title), "1", Number(row.Minutes), Number(row.Score), "" }));
        }
        foreach (var row in reports.Workload)
            lines.Add(string.Join(",", new[] { "workload", row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "", "", "", "", Number(row.Tasks), Number(row.TotalMinutes), "", Number(row.LoadPercent) }));
        return string.Join("\n", lines) + "\n";
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
