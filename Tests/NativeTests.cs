using Tracker.Domain;
using Tracker.Native;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> NativeCases()
    {
        yield return ("Real native call rounds 120 of 180 minutes up to 67 percent", () =>
        {
            NativeValue(new NativeLoadAdapter().Calculate(120, 180), 67);
        });
        yield return ("Real native call supplies both workload report percentages", () =>
        {
            var native = new NativeLoadAdapter();
            NativeValue(native.Calculate(135, 180), 75);
            NativeValue(native.Calculate(20, 180), 12);
            NativeValue(native.Calculate(0, 180), 0);
        });
        yield return ("Real native call handles overload and the largest allowed workload", () =>
        {
            var native = new NativeLoadAdapter();
            NativeValue(native.Calculate(181, 180), 101);
            NativeValue(native.Calculate(1_000_000, 1), 100_000_000);
            NativeValue(native.Calculate(480, 480), 100);
        });
        yield return ("Native adapter rejects invalid domain values before calling native code", () =>
        {
            int calls = 0;
            var native = new NativeLoadAdapter((_, _) => { calls++; return 0; });
            foreach (int minutes in new[] { -1, 1_000_001 })
                NativeFailure(native.Calculate(minutes, 180), "total minutes");
            foreach (int capacity in new[] { 0, 481 })
                NativeFailure(native.Calculate(120, capacity), "available minutes");
            Equal(0, calls);
        });
        yield return ("Native loading and binding failures become clear Result failures", () =>
        {
            Exception[] failures = [new DllNotFoundException(), new EntryPointNotFoundException(), new BadImageFormatException()];
            foreach (Exception failure in failures)
            {
                var native = new NativeLoadAdapter((_, _) => throw failure);
                var result = native.Calculate(120, 180);
                NativeFailure(result, "planner_native/planner_load_percent");
                NativeFailure(result, failure.GetType().Name);
                int continued = 0;
                result.Map(value => { continued++; return value; });
                Equal(0, continued);
            }
        });
        yield return ("A native rejection is not replaced by a managed fallback", () =>
        {
            NativeFailure(new NativeLoadAdapter((_, _) => -1).Calculate(120, 180), "rejected");
        });
    }

    private static void NativeValue(Result<int> result, int expected)
    {
        if (result is not Result<int>.Success success)
            throw new Exception(result is Result<int>.Failure failure ? failure.Error : "Unknown native outcome.");
        Equal(expected, success.Value);
    }

    private static void NativeFailure(Result<int> result, string reason)
    {
        Assert(result is Result<int>.Failure, "Expected a native failure.");
        if (result is Result<int>.Failure failure)
            Assert(failure.Error.Contains(reason, StringComparison.OrdinalIgnoreCase), $"Unexpected native failure: {failure.Error}");
    }
}
