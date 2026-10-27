namespace Tracker.Domain;

public static class TaskExtensions
{
    public static IEnumerable<TaskItem> EligibleOn(
        this IEnumerable<TaskItem> source, DateOnly day, int minimumImportance)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (minimumImportance is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(minimumImportance), "Use importance 1 to 5.");

        return source.Where(task => task.Date == day && !task.Completed
            && task.Importance >= minimumImportance);
    }

    public static IOrderedEnumerable<ScoredTask> RankForPlanning(this IEnumerable<ScoredTask> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.OrderByDescending(item => item.Score)
            .ThenBy(item => item.Task.Id, StringComparer.Ordinal);
    }
}
