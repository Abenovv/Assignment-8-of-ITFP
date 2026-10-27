using Tracker.Domain;
using Tracker.Input;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> FileBoundaryCases()
    {
        yield return ("File boundary loads a controlled valid task file", () => WithTaskDirectory(directory =>
        {
            string path = Path.Combine(directory, "valid.json");
            string text = Record(minutes: 0);
            File.WriteAllText(path, text);
            var loaded = TaskFile.Load(path);
            Assert(loaded is Result<IReadOnlyList<TaskItem>>.Success { Value.Count: 1 }, "Valid file was rejected.");
            if (loaded is Result<IReadOnlyList<TaskItem>>.Success success)
            {
                Equal(0, success.Value[0].Minutes);
                Equal(false, success.Value[0].Completed);
            }
            Equal(text, File.ReadAllText(path));
        }));

        yield return ("A missing task file reports its path and skips dependent operations", () => WithTaskDirectory(directory =>
        {
            string path = Path.Combine(directory, "missing.json");
            var priorPlan = TaskPipeline.CreatePlan(
                [new TaskItem("SAVED", "Existing task", Day, 3, 10, false)], Day, 3, ScoringPolicies.ImportanceFirst);
            PlanningState existing = Changed(PlanningWorkflow.Confirm(
                Changed(PlanningWorkflow.Prepare(PlanningState.Start(priorPlan, 120)))));
            PlanningState savedReference = existing;
            int plans = 0, scores = 0;
            var loaded = TaskFile.Load(path).Map(tasks =>
            {
                plans++;
                var plan = TaskPipeline.CreatePlan(tasks, Day, 3, task =>
                {
                    scores++;
                    return ScoringPolicies.ImportanceFirst(task);
                });
                existing = PlanningState.Start(plan, 120);
                return plan;
            });
            AssertFileFailure(loaded, path, "file not found");
            Equal(0, plans);
            Equal(0, scores);
            Assert(ReferenceEquals(savedReference, existing), "A missing file replaced the previous state.");
            Assert(existing is PlanningState.Confirmed { Session.Plan.TotalMinutes: 10 }, "The saved confirmation changed.");
            Assert(!File.Exists(path), "Reading a missing file created it.");
        }));

        yield return ("A missing task directory becomes an explicit failure", () => WithTaskDirectory(directory =>
        {
            string path = Path.Combine(directory, "absent-directory", "tasks.json");
            AssertFileFailure(TaskFile.Load(path), path, "directory not found");
            Assert(!Directory.Exists(Path.GetDirectoryName(path)), "Read created a missing directory.");
        }));

        yield return ("Malformed JSON file is rejected with file and parsing context", () => WithTaskDirectory(directory =>
        {
            string path = Path.Combine(directory, "broken.json");
            const string text = "[{\"id\": \"T1\", this is not JSON]";
            File.WriteAllText(path, text);
            int continued = 0;
            var loaded = TaskFile.Load(path).Bind(tasks =>
            {
                continued++;
                return Result<int>.Ok(tasks.Count);
            });
            AssertFileFailure(loaded, path, "Invalid JSON");
            Equal(0, continued);
            Equal(text, File.ReadAllText(path));
        }));

        yield return ("Wrong field types in a file do not become a successful empty dataset", () => WithTaskDirectory(directory =>
        {
            string path = Path.Combine(directory, "wrong-type.json");
            string text = Record().Replace("\"importance\":3", "\"importance\":\"high\"");
            File.WriteAllText(path, text);
            AssertFileFailure(TaskFile.Load(path), path, "importance");
            Equal(text, File.ReadAllText(path));
        }));

        yield return ("A later invalid record cannot replace an existing confirmed plan", () => WithTaskDirectory(directory =>
        {
            var originalTasks = new[] { new TaskItem("SAVED", "Existing task", Day, 3, 10, false) };
            var originalPlan = TaskPipeline.CreatePlan(originalTasks, Day, 3, ScoringPolicies.ImportanceFirst);
            var prepared = Changed(PlanningWorkflow.Prepare(PlanningState.Start(originalPlan, 120)));
            PlanningState existing = Changed(PlanningWorkflow.Confirm(prepared));
            PlanningState savedReference = existing;
            var savedTasks = originalTasks.ToArray();
            string path = Path.Combine(directory, "partially-valid.json");
            string validRecord = Record()[1..^1];
            string invalidRecord = Record(id: "T2", minutes: -1)[1..^1];
            string text = $"[{validRecord},{invalidRecord}]";
            File.WriteAllText(path, text);
            int updates = 0;
            var loaded = TaskFile.Load(path).Map(tasks =>
            {
                updates++;
                existing = PlanningState.Start(TaskPipeline.CreatePlan(tasks, Day, 3, ScoringPolicies.ImportanceFirst), 120);
                return existing;
            });
            AssertFileFailure(loaded, path, "Record 2");
            Equal(0, updates);
            Assert(ReferenceEquals(savedReference, existing), "A failed load replaced the confirmed state.");
            Assert(existing is PlanningState.Confirmed { Session.Plan.TotalMinutes: 10 }, "Confirmation was lost.");
            Sequence(savedTasks, originalTasks);
            Equal(text, File.ReadAllText(path));
        }));

        yield return ("An empty file is a visible failure rather than a fallback plan", () => WithTaskDirectory(directory =>
        {
            string path = Path.Combine(directory, "empty-file.json");
            File.WriteAllText(path, "");
            AssertFileFailure(TaskFile.Load(path), path, "JSON array is required");
            Equal("", File.ReadAllText(path));
        }));

        yield return ("An invalid file path is handled at the file boundary", () =>
        {
            var result = TaskFile.Load("");
            Assert(result is Result<IReadOnlyList<TaskItem>>.Failure failure
                && failure.Error.Contains("Cannot read task file"), "An empty path escaped the file boundary.");
        });
    }

    private static void AssertFileFailure<T>(Result<T> result, string path, string reason)
    {
        Assert(result is Result<T>.Failure, "Expected a file-load failure.");
        if (result is Result<T>.Failure failure)
        {
            Assert(failure.Error.Contains(path, StringComparison.Ordinal), "Failure omitted its file path.");
            Assert(failure.Error.Contains(reason, StringComparison.OrdinalIgnoreCase), $"Unexpected failure: {failure.Error}");
        }
    }

    private static void WithTaskDirectory(Action<string> check)
    {
        // Each test owns this fresh directory and removes only the files it creates.
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dailyplanner-a7-");
        try { check(directory.FullName); }
        finally { directory.Delete(recursive: true); }
    }
}
