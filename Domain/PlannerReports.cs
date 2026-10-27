namespace Tracker.Domain;

public sealed record FocusRow(string Id, string Title, int Minutes, int Score, int Position);
public sealed record DayWorkload(DateOnly Date, int Tasks, int TotalMinutes);

public static class PlannerReports
{
    public static IReadOnlyList<FocusRow> DailyFocus(
        IEnumerable<TaskItem> tasks, DateOnly day, int minimumImportance, Func<TaskItem, int> scorePolicy)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(scorePolicy);
        if (minimumImportance is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(minimumImportance), "Minimum importance must be between 1 and 5.");

        // Enumerate and score once; later ordering uses this stable snapshot.
        var scored = LazyPlanning.ScoreEligible(tasks, day, minimumImportance, scorePolicy).ToArray();
        var rows = scored.RankForPlanning()
            .Select((item, index) => new FocusRow(item.Task.Id, item.Task.Title, item.Task.Minutes, item.Score, index + 1))
            .ToArray();
        return Array.AsReadOnly(rows);
    }

    public static IReadOnlyList<DayWorkload> WorkloadByDay(
        IEnumerable<TaskItem> tasks, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (from > to)
            throw new ArgumentException("The report start date must not follow its end date.", nameof(from));

        var rows = tasks
            .Where(task => !task.Completed && task.Date >= from && task.Date <= to)
            .GroupBy(task => task.Date)
            .Select(group => new DayWorkload(group.Key, group.Count(), group.Sum(task => task.Minutes)))
            .OrderByDescending(day => day.TotalMinutes)
            .ThenBy(day => day.Date)
            .ToArray();
        return Array.AsReadOnly(rows);
    }
}
