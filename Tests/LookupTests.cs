using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> LookupCases()
    {
        yield return ("Lookup finds the requested task and explicitly marks absence", () =>
        {
            var tasks = Sample();
            Assert(TaskLookup.Find(tasks, " T3 ") is Option<TaskItem>.Some { Value.Id: "T3" }, "Lookup missed T3.");
            Assert(TaskLookup.Find(tasks, "absent") is Option<TaskItem>.None, "Missing task became a record.");
            Assert(TaskLookup.Find(tasks, "t3") is Option<TaskItem>.None, "IDs should be case-sensitive.");
        });
        yield return ("Optional lookup keeps zero minutes and false as present values", () =>
        {
            TaskItem[] tasks = [new("REM", "Reminder", Day, 3, 0, false)];
            var found = TaskLookup.Find(tasks, "REM");
            Assert(found.Map(task => task.Minutes) is Option<int>.Some { Value: 0 }, "Zero became missing.");
            Assert(found.Map(task => task.Completed) is Option<bool>.Some { Value: false }, "False became missing.");
            Assert(TaskLookup.Find(tasks, "absent").Map(task => task.Minutes) is Option<int>.None, "Missing became zero.");
        });
        yield return ("Option keeps empty text present and skips transformations on absence", () =>
        {
            Assert(Option<string>.Present("") is Option<string>.Some { Value: "" }, "Empty text became missing.");
            int calls = 0;
            var absent = Option<int>.Missing().Map(value => { calls++; return value + 1; });
            Assert(absent is Option<int>.None, "None changed to Some.");
            Equal(0, calls);
            Throws<ArgumentNullException>(() => Option<string?>.Present(null));
        });
        yield return ("Required lookup converts missing IDs to useful failures", () =>
        {
            RequestFailure(TaskLookup.Required(Sample(), "absent"), "absent", "not found");
            RequestFailure(TaskLookup.Required(Sample(), " "), "ID", "required");
        });

    }
}
