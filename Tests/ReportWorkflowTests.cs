using Tracker.Application;
using Tracker.Domain;
using Tracker.Native;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> ReportWorkflowCases()
    {
        yield return ("Complete reports use actual native values and export the expected CSV", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            var result = ReportWorkflow.Run(input, output, Day, 3, 180);
            if (result is not Result<ReportRun>.Success success)
                throw new Exception("Report workflow failed: " + ReportError(result));
            Sequence(new[] { "T1", "T2", "T7", "T3" }, success.Value.Reports.Importance.Select(row => row.Id));
            Sequence(new[] { "T3", "T7", "T2", "T1" }, success.Value.Reports.ShortTasks.Select(row => row.Id));
            Equal(120, success.Value.Reports.Importance.Sum(row => row.Minutes));
            Sequence(new[] { 75, 12 }, success.Value.Reports.Workload.Select(row => row.LoadPercent));
            string csv = File.ReadAllText(output);
            Assert(csv.Contains("workload,2026-10-13,,,,,5,135,,75"), csv);
            Assert(csv.Contains("workload,2026-10-14,,,,,1,20,,12"), csv);
            Equal(11, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            Equal(ReportPresentation.ToCsv(success.Value.Reports), csv);
            using var reopened = new FileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }));

        yield return ("Complete native failure preserves the existing export and input data", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            string original = File.ReadAllText(input);
            File.WriteAllText(output, "old export");
            int calls = 0;
            var adapter = new NativeLoadAdapter((_, _) => { calls++; throw new DllNotFoundException("Controlled missing native library."); });
            var result = ReportWorkflow.Run(input, output, Day, 3, 180, adapter);
            Assert(result is Result<ReportRun>.Failure, "Native failure became success.");
            Assert(ReportError(result).Contains("DllNotFoundException"), ReportError(result));
            Equal(1, calls); // The second day's call is skipped after the first failed.
            Equal("old export", File.ReadAllText(output));
            Equal(original, File.ReadAllText(input));
            Equal(2, Directory.GetFiles(directory).Length);
        }));

        yield return ("Malformed report input skips native work and preserves the old export", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            File.WriteAllText(input, "[{ broken");
            File.WriteAllText(output, "old export");
            int calls = 0;
            var result = ReportWorkflow.Run(input, output, Day, 3, 180,
                new NativeLoadAdapter((_, _) => { calls++; return 0; }));
            Assert(ReportError(result).Contains(input), ReportError(result));
            Assert(ReportError(result).Contains("Invalid JSON"), ReportError(result));
            Equal(0, calls);
            Equal("old export", File.ReadAllText(output));
            Equal("[{ broken", File.ReadAllText(input));
        }));

        yield return ("Missing report file skips native calculation and creates no export", () => WithReportDirectory(directory =>
        {
            string input = Path.Combine(directory, "missing.json"), output = Path.Combine(directory, "report.csv");
            int calls = 0;
            var result = ReportWorkflow.Run(input, output, Day, 3, 180,
                new NativeLoadAdapter((_, _) => { calls++; return 0; }));
            Assert(ReportError(result).Contains("file not found"), ReportError(result));
            Equal(0, calls);
            Assert(!File.Exists(output), "A failed workflow wrote an export.");
        }));

        yield return ("Empty reports export a header and do not fabricate native workload rows", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            File.WriteAllText(input, "[]");
            int calls = 0;
            var result = ReportWorkflow.Run(input, output, Day, 3, 180,
                new NativeLoadAdapter((_, _) => { calls++; return 0; }));
            Assert(result is Result<ReportRun>.Success { Value.Reports.Importance.Count: 0, Value.Reports.Workload.Count: 0 }, "Empty reports failed.");
            Equal(0, calls);
            Equal(1, File.ReadAllText(output).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }));

        yield return ("Invalid report settings fail even when the input dataset is empty", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            File.WriteAllText(input, "[]");
            Assert(ReportError(ReportWorkflow.Run(input, output, Day, 3, 0)).Contains("available minutes"), "Invalid capacity accepted.");
            Assert(ReportError(ReportWorkflow.Run(input, output, Day, 0, 180)).Contains("importance"), "Invalid threshold accepted.");
            Assert(ReportError(ReportWorkflow.Run(input, output, DateOnly.MaxValue, 3, 180)).Contains("following day"), "Invalid date window accepted.");
            Assert(!File.Exists(output), "Invalid settings wrote output.");
        }));

        yield return ("CSV export quotes task titles containing commas and quotes", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            File.WriteAllText(input, System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { id = "CSV", title = "Read, then \"write\"", date = "2026-10-13", importance = 3, minutes = 10, completed = false }
            }));
            Assert(ReportWorkflow.Run(input, output, Day, 3, 180) is Result<ReportRun>.Success, "CSV workflow failed.");
            Assert(File.ReadAllText(output).Contains("\"Read, then \"\"write\"\"\""), "CSV field was not escaped.");
        }));

        yield return ("Report export failure returns a failure without changing the input", () => WithReportDirectory(directory =>
        {
            var (input, _) = ReportPaths(directory);
            string original = File.ReadAllText(input);
            string outputDirectory = Path.Combine(directory, "destination");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "keep.txt"), "unchanged");
            var result = ReportWorkflow.Run(input, outputDirectory, Day, 3, 180);
            Assert(result is Result<ReportRun>.Failure, "Directory overwrite became success.");
            Equal(original, File.ReadAllText(input));
            Equal("unchanged", File.ReadAllText(Path.Combine(outputDirectory, "keep.txt")));
            Equal(1, Directory.GetFiles(directory).Length); // Input only; no abandoned temporary export.
        }));

        yield return ("Invalid task data is rejected before either report or native calculation", () => WithReportDirectory(directory =>
        {
            var (input, output) = ReportPaths(directory);
            File.WriteAllText(input, Record(minutes: -1));
            int calls = 0;
            var result = ReportWorkflow.Run(input, output, Day, 3, 180,
                new NativeLoadAdapter((_, _) => { calls++; return 0; }));
            Assert(ReportError(result).Contains("minutes"), ReportError(result));
            Equal(0, calls);
            Assert(!File.Exists(output), "Invalid data wrote a report.");
        }));
    }

    private static string ReportError(Result<ReportRun> result) => result is Result<ReportRun>.Failure failure
        ? failure.Error : "Expected failure, got success.";

    private static (string Input, string Output) ReportPaths(string directory)
    {
        string input = Path.Combine(directory, "tasks.json"), output = Path.Combine(directory, "report.csv");
        File.WriteAllText(input, File.ReadAllText("samples/tasks.json"));
        return (input, output);
    }

    private static void WithReportDirectory(Action<string> test)
    {
        var directory = Directory.CreateTempSubdirectory("dailyplanner-report-");
        try { test(directory.FullName); }
        finally { directory.Delete(recursive: true); }
    }
}
