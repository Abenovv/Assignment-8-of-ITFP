using System.Runtime.InteropServices;
using Tracker.Domain;

namespace Tracker.Native;

public sealed class NativeLoadAdapter
{
    private readonly Func<int, int, int> calculate;

    // Production uses P/Invoke. Tests can inject a controlled loading failure.
    public NativeLoadAdapter(Func<int, int, int>? nativeCall = null) =>
        calculate = nativeCall ?? NativeMethods.LoadPercent;

    public Result<int> Calculate(int totalMinutes, int availableMinutes)
    {
        if (totalMinutes is < 0 or > 1_000_000)
            return Result<int>.Fail("Native workload requires total minutes from 0 to 1000000.");
        if (availableMinutes is < 1 or > 480)
            return Result<int>.Fail("Native workload requires available minutes from 1 to 480.");

        try
        {
            int percentage = calculate(totalMinutes, availableMinutes);
            return percentage < 0
                ? Result<int>.Fail("Native planner_load_percent rejected the workload arguments.")
                : Result<int>.Ok(percentage);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Result<int>.Fail(
                $"Cannot call native planner_native/planner_load_percent on {RuntimeInformation.OSDescription} " +
                $"({RuntimeInformation.ProcessArchitecture}): {error.GetType().Name}. " +
                "Build native/build.ps1 on Windows or native/build.sh on macOS/Linux, then rebuild the solution.");
        }
    }
}
