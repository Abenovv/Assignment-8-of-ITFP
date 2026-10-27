using Tracker.Domain;

namespace Tracker.Input;

// File access belongs to this application boundary, never to the domain pipeline.
public static class TaskFile
{
    public static Result<IReadOnlyList<TaskItem>> Load(string path) =>
        Read(path).Bind(json => AddFileContext(path, TaskParser.Parse(json)));

    public static Result<string> Read(string path)
    {
        try
        {
            return Result<string>.Ok(File.ReadAllText(path));
        }
        catch (FileNotFoundException)
        {
            return Result<string>.Fail($"Cannot read task file '{path}': file not found.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<string>.Fail($"Cannot read task file '{path}': directory not found.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<string>.Fail($"Cannot read task file '{path}': access denied.");
        }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException)
        {
            return Result<string>.Fail($"Cannot read task file '{path}': {error.Message}");
        }
    }

    private static Result<IReadOnlyList<TaskItem>> AddFileContext(
        string path, Result<IReadOnlyList<TaskItem>> parsed) => parsed switch
    {
        Result<IReadOnlyList<TaskItem>>.Success => parsed,
        Result<IReadOnlyList<TaskItem>>.Failure failure =>
            Result<IReadOnlyList<TaskItem>>.Fail($"Cannot load task file '{path}': {failure.Error}"),
        _ => throw new InvalidOperationException("Unknown parser result.")
    };
}
