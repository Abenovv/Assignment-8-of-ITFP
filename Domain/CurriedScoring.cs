namespace Tracker.Domain;

public static class CurriedScoring
{
    public static readonly Func<TaskItem, int> ImportanceFirst = Create(10)(0);
    public static readonly Func<TaskItem, int> ShortTasksFirst = Create(10)(60);

    public static Func<int, Func<TaskItem, int>> Create(int importanceWeight)
    {
        if (importanceWeight is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(importanceWeight), "Importance weight must be between 1 and 100.");

        return bonusWindow =>
        {
            if (bonusWindow is < 0 or > 480)
                throw new ArgumentOutOfRangeException(nameof(bonusWindow), "Bonus window must be between 0 and 480 minutes.");

            return task =>
            {
                ArgumentNullException.ThrowIfNull(task);
                return task.Importance * importanceWeight + Math.Max(0, bonusWindow - task.Minutes);
            };
        };
    }
}
