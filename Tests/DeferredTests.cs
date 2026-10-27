using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> DeferredCases()
    {
        yield return ("Creating a lazy task query and enumerator does not process data", () =>
        {
            int reads = 0, scores = 0, disposals = 0;
            var source = ProbeTaskReads(Sample(), () => reads++, () => disposals++);
            var query = LazyPlanning.ScoreEligible(source, Day, 3,
                task => { scores++; return ScoringPolicies.ImportanceFirst(task); });
            Equal(0, reads);
            Equal(0, scores);
            using (var iterator = query.GetEnumerator())
            {
                Equal(0, reads);
                Equal(0, scores);
                Assert(iterator.MoveNext(), "First task is missing.");
                Equal("T1", iterator.Current.Task.Id);
                Equal(50, iterator.Current.Score);
                Equal(1, reads);
                Equal(1, scores);
            }
            Equal(1, disposals);
        });

        yield return ("Stopping a lazy task query early releases its source enumerator", () =>
        {
            int scores = 0, disposals = 0;
            var source = ProbeTaskReads(Sample(), () => { }, () => disposals++);
            var query = LazyPlanning.ScoreEligible(source, Day, 3,
                task => { scores++; return ScoringPolicies.ShortTasksFirst(task); });
            Sequence(new[] { "T1", "T2" }, query.Take(2).Select(item => item.Task.Id).ToArray());
            Equal(2, scores);
            Equal(1, disposals);
        });

        yield return ("Materialising lazy scores gives a predictable reusable snapshot", () =>
        {
            int scores = 0;
            var tasks = Sample().ToList();
            var query = LazyPlanning.ScoreEligible(tasks, Day, 3,
                task => { scores++; return ScoringPolicies.ImportanceFirst(task); });
            var snapshot = query.ToArray();
            Equal(4, scores);
            tasks.Clear();
            Sequence(new[] { "T1", "T2", "T3", "T7" }, snapshot.Select(item => item.Task.Id));
            Equal(120, snapshot.Sum(item => item.Task.Minutes));
            Equal(160, snapshot.Sum(item => item.Score));
            Equal(4, scores);
        });

        yield return ("Repeated raw enumeration repeats calculations but not state changes", () =>
        {
            int scores = 0, disposals = 0;
            var tasks = Sample().ToArray();
            var before = tasks.ToArray();
            var source = ProbeTaskReads(tasks, () => { }, () => disposals++);
            var query = LazyPlanning.ScoreEligible(source, Day, 3,
                task => { scores++; return ScoringPolicies.ImportanceFirst(task); });
            var first = query.ToArray();
            var second = query.ToArray();
            Sequence(first, second);
            Equal(8, scores);
            Equal(2, disposals);
            Sequence(before, tasks);
        });

        yield return ("A processing exception still disposes the lazy source", () =>
        {
            int disposals = 0, scores = 0;
            var source = ProbeTaskReads(Sample(), () => { }, () => disposals++);
            var query = LazyPlanning.ScoreEligible(source, Day, 3, task =>
            {
                if (++scores == 2) throw new InvalidOperationException("Controlled test failure.");
                return task.Importance * 10;
            });
            Throws<InvalidOperationException>(() => query.ToArray());
            Equal(2, scores);
            Equal(1, disposals);
        });
    }

    private static IEnumerable<TaskItem> ProbeTaskReads(
        IEnumerable<TaskItem> tasks, Action read, Action disposed)
    {
        try
        {
            foreach (var task in tasks)
            {
                read();
                yield return task;
            }
        }
        finally { disposed(); }
    }
}
