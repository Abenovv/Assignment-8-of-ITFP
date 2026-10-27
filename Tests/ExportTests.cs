using System.Text;
using Tracker.Domain;
using Tracker.Input;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> ExportCases()
    {
        yield return ("Report export writes exact UTF8 content and releases its file handle", () => WithExportDirectory(directory =>
        {
            string path = Path.Combine(directory, "report.csv");
            const string csv = "Task,Minutes\nПроверить расписание,0\n";
            var result = ReportExporter.Save(path, csv);
            Assert(result is Result<string>.Success { Value: var saved } && saved == Path.GetFullPath(path),
                result is Result<string>.Failure error ? error.Error : "Export did not return the saved report path.");
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var reader = new StreamReader(exclusive, Encoding.UTF8))
                Equal(csv, reader.ReadToEnd());
            Sequence(new[] { path }, Directory.GetFiles(directory));
        }));

        yield return ("A successful report export replaces an earlier report completely", () => WithExportDirectory(directory =>
        {
            string path = Path.Combine(directory, "report.csv");
            File.WriteAllText(path, "previous report");
            var result = ReportExporter.Save(path, "Date,Minutes\n2026-10-13,135\n");
            Assert(result is Result<string>.Success,
                result is Result<string>.Failure error ? error.Error : "The report could not be replaced.");
            Equal("Date,Minutes\n2026-10-13,135\n", File.ReadAllText(path));
            Sequence(new[] { path }, Directory.GetFiles(directory));
        }));

        yield return ("Failed report replacement disposes and removes the temporary file", () => WithExportDirectory(directory =>
        {
            string targetDirectory = Path.Combine(directory, "report.csv");
            Directory.CreateDirectory(targetDirectory);
            string sentinel = Path.Combine(targetDirectory, "keep.txt");
            File.WriteAllText(sentinel, "unchanged");
            AssertExportFailure(ReportExporter.Save(targetDirectory, "new report"), "Cannot export report");
            Equal("unchanged", File.ReadAllText(sentinel));
            Equal(0, Directory.GetFiles(directory).Length);
            // A leaked stream on Windows would prevent deletion of the temporary file above.
            Equal(0, Directory.GetFiles(directory, ".planner-report-*.tmp").Length);
        }));

        yield return ("Report export refuses the normalized input path without modifying data", () => WithExportDirectory(directory =>
        {
            string inputPath = Path.Combine(directory, "tasks.json");
            const string original = "[{\"id\":\"T1\"}]";
            File.WriteAllText(inputPath, original);
            string samePath = Path.Combine(directory, ".", "tasks.json");
            AssertExportFailure(ReportExporter.Save(samePath, "report text", inputPath), "input data file");
            Equal(original, File.ReadAllText(inputPath));
            Sequence(new[] { inputPath }, Directory.GetFiles(directory));
        }));

        yield return ("A denied report replacement preserves the prior report", () => WithExportDirectory(directory =>
        {
            string path = Path.Combine(directory, "report.csv");
            File.WriteAllText(path, "previous report");
            if (OperatingSystem.IsWindows())
            {
                using var heldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                AssertExportFailure(ReportExporter.Save(path, "replacement"), "Cannot export report");
                Equal("previous report", File.ReadAllText(path));
                Equal(0, Directory.GetFiles(directory, ".planner-report-*.tmp").Length);
            }
            else
            {
                // Unix permits renaming an open file. Exercise the same preservation contract
                // through the explicit protected-source rejection instead of assuming Windows locks.
                AssertExportFailure(ReportExporter.Save(path, "replacement", path), "input data file");
                Equal("previous report", File.ReadAllText(path));
            }
        }));

        yield return ("Missing export destination becomes an explicit failure", () =>
        {
            AssertExportFailure(ReportExporter.Save(" ", "report text"), "destination is required");
        });
    }

    private static void AssertExportFailure(Result<string> result, string expected)
    {
        if (result is not Result<string>.Failure failure)
            throw new Exception("Expected an export failure.");
        Assert(failure.Error.Contains(expected, StringComparison.OrdinalIgnoreCase), failure.Error);
    }

    private static void WithExportDirectory(Action<string> check)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dailyplanner-export-");
        try { check(directory.FullName); }
        finally { directory.Delete(recursive: true); }
    }
}
