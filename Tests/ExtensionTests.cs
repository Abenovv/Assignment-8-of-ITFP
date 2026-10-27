using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> ExtensionCases()
    {
        yield return ("EligibleOn combines date completion and importance rules", () =>
        {
            var tasks = Sample().ToList();
            var original = tasks.ToArray();
            Sequence(new[] { "T1", "T2", "T3", "T7" }, tasks.EligibleOn(Day, 3).Select(task => task.Id));
            Sequence(new[] { "T1", "T2", "T7" }, tasks.EligibleOn(Day, 4).Select(task => task.Id));
            Sequence(original, tasks);
        });

        yield return ("EligibleOn supports empty and no-match data", () =>
        {
            Equal(0, Array.Empty<TaskItem>().EligibleOn(Day, 3).Count());
            Equal(0, Sample().EligibleOn(Day.AddYears(1), 3).Count());
        });

        yield return ("EligibleOn rejects invalid thresholds before enumeration", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => Array.Empty<TaskItem>().EligibleOn(Day, 0));
            Throws<ArgumentOutOfRangeException>(() => Array.Empty<TaskItem>().EligibleOn(Day, 6));
        });

        yield return ("RankForPlanning orders score then ordinal ID without mutation", () =>
        {
            var task = new TaskItem("B", "Read", Day, 3, 10, false);
            ScoredTask[] scored = [new(task, 80), new(task with { Id = "X" }, 30), new(task with { Id = "A" }, 80)];
            var original = scored.ToArray();
            Sequence(new[] { "A", "B", "X" }, scored.RankForPlanning().Select(item => item.Task.Id));
            Sequence(original, scored);
            Equal(0, Array.Empty<ScoredTask>().RankForPlanning().Count());
        });
    }
}
