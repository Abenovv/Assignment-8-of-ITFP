namespace Tracker.Domain;

public static class TaskLookup
{
    public static Option<TaskItem> Find(IReadOnlyList<TaskItem> tasks, string? id)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (string.IsNullOrWhiteSpace(id))
            return Option<TaskItem>.Missing();

        string requested = id.Trim();
        foreach (var task in tasks)
            if (string.Equals(task.Id, requested, StringComparison.Ordinal))
                return Option<TaskItem>.Present(task);
        return Option<TaskItem>.Missing();
    }

    public static Result<TaskItem> Required(IReadOnlyList<TaskItem> tasks, string? id) =>
        Find(tasks, id) switch
        {
            Option<TaskItem>.Some found => Result<TaskItem>.Ok(found.Value),
            Option<TaskItem>.None => Result<TaskItem>.Fail(string.IsNullOrWhiteSpace(id)
                ? "A task ID is required."
                : $"Task '{id.Trim()}' was not found."),
            _ => throw new InvalidOperationException("Unknown lookup outcome.")
        };
}
