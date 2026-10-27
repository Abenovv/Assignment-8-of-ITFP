using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> ConfigurationCases()
    {
        yield return ("Two partially applied scores differ for the same task", () =>
        {
            var task = new TaskItem("T3", "Plan tomorrow", Day, 3, 10, false);
            var withWeight = CurriedScoring.Create(10);
            var importance = withWeight(0);
            var shortTasks = withWeight(60);
            Equal(30, importance(task));
            Equal(80, shortTasks(task));
            Equal(30, CurriedScoring.ImportanceFirst(task));
            Equal(80, CurriedScoring.ShortTasksFirst(task));
        });
        yield return ("Configured delegates change the focus report on one dataset", () =>
        {
            var tasks = Sample();
            var importance = PlannerReports.DailyFocus(tasks, Day, 3, CurriedScoring.ImportanceFirst);
            var shortTasks = PlannerReports.DailyFocus(tasks, Day, 3, CurriedScoring.ShortTasksFirst);
            Sequence(new[] { "T1", "T2", "T7", "T3" }, importance.Select(row => row.Id));
            Sequence(new[] { "T3", "T7", "T2", "T1" }, shortTasks.Select(row => row.Id));
            Sequence(new[] { 80, 80, 70, 50 }, shortTasks.Select(row => row.Score));
            Equal(120, importance.Sum(row => row.Minutes));
            Equal(120, shortTasks.Sum(row => row.Minutes));
        });
        yield return ("Configured functions preserve zero and clamp the duration bonus", () =>
        {
            var reminder = new TaskItem("R0", "Reminder", Day, 3, 0, false);
            Equal(90, CurriedScoring.Create(10)(60)(reminder));
            Equal(30, CurriedScoring.Create(10)(60)(reminder with { Minutes = 60 }));
            Equal(30, CurriedScoring.Create(10)(60)(reminder with { Minutes = 480 }));
            Equal(780, CurriedScoring.Create(100)(480)(reminder));
        });
        yield return ("Curried configuration rejects invalid weights and windows", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => CurriedScoring.Create(0));
            Throws<ArgumentOutOfRangeException>(() => CurriedScoring.Create(101));
            Throws<ArgumentOutOfRangeException>(() => CurriedScoring.Create(10)(-1));
            Throws<ArgumentOutOfRangeException>(() => CurriedScoring.Create(10)(481));
        });
    }
}
