namespace Tracker.Domain;

public static class LazyPlanning
{
    public static IEnumerable<ScoredTask> ScoreEligible(
        IEnumerable<TaskItem> tasks, DateOnly day, int minimumImportance,
        Func<TaskItem, int> scorePolicy)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(scorePolicy);
        var eligible = tasks.EligibleOn(day, minimumImportance);
        return Score(eligible, scorePolicy);
    }

    private static IEnumerable<ScoredTask> Score(
        IEnumerable<TaskItem> tasks, Func<TaskItem, int> scorePolicy)
    {
        foreach (var task in tasks)
            yield return new ScoredTask(task, scorePolicy(task));
    }
}
