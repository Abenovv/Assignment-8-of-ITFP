using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> ReportCases()
    {
        yield return ("Daily focus report gives the documented rows and positions", () =>
        {
            var rows = PlannerReports.DailyFocus(Sample(), Day, 3, CurriedScoring.ImportanceFirst);
            Sequence(new[] { "T1", "T2", "T7", "T3" }, rows.Select(row => row.Id));
            Sequence(new[] { 50, 40, 40, 30 }, rows.Select(row => row.Score));
            Sequence(new[] { 1, 2, 3, 4 }, rows.Select(row => row.Position));
            Equal("Review FP notes", rows[0].Title);
            Equal(120, rows.Sum(row => row.Minutes));
        });
        yield return ("Workload report groups actual pending tasks and totals", () =>
        {
            var rows = PlannerReports.WorkloadByDay(Sample(), Day, Day.AddDays(1));
            Equal(2, rows.Count);
            Equal(new DayWorkload(Day, 5, 135), rows[0]);
            Equal(new DayWorkload(Day.AddDays(1), 1, 20), rows[1]);
            Equal(6, rows.Sum(row => row.Tasks));
            Equal(155, rows.Sum(row => row.TotalMinutes));
        });
        yield return ("Both reports handle an empty dataset", () =>
        {
            Equal(0, PlannerReports.DailyFocus([], Day, 3, CurriedScoring.ImportanceFirst).Count);
            Equal(0, PlannerReports.WorkloadByDay([], Day, Day).Count);
        });
        yield return ("Report filters can produce no matching records", () =>
        {
            var completed = new TaskItem("DONE", "Already finished", Day, 5, 20, true);
            Equal(0, PlannerReports.DailyFocus([completed], Day, 1, CurriedScoring.ShortTasksFirst).Count);
            Equal(0, PlannerReports.WorkloadByDay([completed], Day, Day).Count);
            Equal(0, PlannerReports.DailyFocus(Sample(), Day.AddDays(2), 1, CurriedScoring.ImportanceFirst).Count);
            Equal(0, PlannerReports.WorkloadByDay(Sample(), Day.AddDays(2), Day.AddDays(3)).Count);
        });
        yield return ("Focus ties use ordinal ID rather than source order", () =>
        {
            var task = new TaskItem("B", "Task B", Day, 3, 10, false);
            var rows = PlannerReports.DailyFocus([task, task with { Id = "A" }], Day, 1, CurriedScoring.ImportanceFirst);
            Sequence(new[] { "A", "B" }, rows.Select(row => row.Id));
            Sequence(new[] { 1, 2 }, rows.Select(row => row.Position));
        });
        yield return ("Equal workload totals use date as the tie breaker", () =>
        {
            TaskItem[] tasks =
            [
                new("B", "Tomorrow", Day.AddDays(1), 3, 10, false),
                new("A", "Today", Day, 3, 10, false)
            ];
            var rows = PlannerReports.WorkloadByDay(tasks, Day, Day.AddDays(1));
            Sequence(new[] { Day, Day.AddDays(1) }, rows.Select(row => row.Date));
        });
        yield return ("Workload date limits are inclusive and exclude other dates", () =>
        {
            var rows = PlannerReports.WorkloadByDay(Sample(), Day.AddDays(1), Day.AddDays(1));
            Equal(1, rows.Count);
            Equal(new DayWorkload(Day.AddDays(1), 1, 20), rows[0]);
        });
        yield return ("Reports preserve input and return read only snapshots", () =>
        {
            var tasks = Sample().ToList();
            var original = tasks.ToArray();
            var focus = PlannerReports.DailyFocus(tasks, Day, 3, CurriedScoring.ImportanceFirst);
            var workload = PlannerReports.WorkloadByDay(tasks, Day, Day.AddDays(1));
            Sequence(original, tasks);
            tasks.Clear();
            Equal(4, focus.Count);
            Equal(2, workload.Count);
            Throws<NotSupportedException>(() => ((IList<FocusRow>)focus).Clear());
            Throws<NotSupportedException>(() => ((IList<DayWorkload>)workload).Clear());
        });
        yield return ("Invalid report configuration is rejected explicitly", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => PlannerReports.DailyFocus(Sample(), Day, 0, CurriedScoring.ImportanceFirst));
            Throws<ArgumentOutOfRangeException>(() => PlannerReports.DailyFocus(Sample(), Day, 6, CurriedScoring.ImportanceFirst));
            Throws<ArgumentException>(() => PlannerReports.WorkloadByDay(Sample(), Day.AddDays(1), Day));
        });
    }
}
