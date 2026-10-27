using System.Runtime.InteropServices;

namespace Tracker.Native;

internal static class NativeMethods
{
    [DllImport("planner_native", EntryPoint = "planner_load_percent",
        CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int LoadPercent(int totalMinutes, int availableMinutes);
}
