# DailyPlanner — Assignment 8

Yerkebulan Abenov, Nurbergen Assan, Amanalina Tumargul and Khalid Ahmad Ahmadi — **IT-2511**.

Repository: [Assignment-8-of-ITFP](https://github.com/Abenovv/Assignment-8-of-ITFP).

This is the same student task planner used in Assignments 5–7. The earlier selection, scoring and ranking pipeline, validated planning states, Option lookup and Result error handling remain active. Assignment 8 adds two reports, configured scoring functions, lazy scoring, a CSV export and a small C library used by the workload report.

## Build and run

The verified platform is **Windows x64**, using **.NET SDK 10.0.401** and **MSYS2 UCRT64 GCC 14.2.0** for the native build. The ZIP includes the Windows x64 DLL in `runtimes/win-x64/native/planner_native.dll`, so a C compiler is not needed just to build and run the supplied Windows project. No database or external NuGet packages are required.

Extract the project and open Terminal or PowerShell in the folder containing `DailyPlanner.sln`. Run:

```text
dotnet build DailyPlanner.sln --disable-build-servers --maxcpucount:1
dotnet run --project Tests/DailyPlanner.Tests.csproj --no-build
dotnet run --project DailyPlanner.csproj --no-build -- --reports samples/tasks.json output/planner-report.csv 180 2026-10-13 3
```

The second command runs assertion tests and exits 1 if any assertion fails. Use it instead of `dotnet test`; this project uses its own test runner. The report command displays both reports and saves a UTF-8 CSV file. It exits 0 only after the complete operation succeeds.

Verification on October 27, 2026: the solution builds with **0 warnings and 0 errors**, and **111/111 cumulative tests pass**: 68 retained tests and 43 new tests for this stage.

`Directory.Build.targets` chooses the native asset using the .NET SDK runtime identifier and copies it into both application and test output folders. It reports a clear build error when the required platform binary is absent. The source tree keeps native binaries under `runtimes`, separate from generated .NET `bin` and `obj` folders.

To run the retained planner and state workflow:

```text
dotnet run --project DailyPlanner.csproj --no-build
dotnet run --project DailyPlanner.csproj --no-build -- --task T3
dotnet run --project DailyPlanner.csproj --no-build -- --lookup UNKNOWN
dotnet run --project DailyPlanner.csproj --no-build -- --error-demo
```

The default planner still selects four tasks totaling 120 minutes and confirms each policy's plan within a 180-minute budget. An optional missing lookup reports absence; a required missing task returns a failure. Preparation and confirmation still reject invalid prerequisites and repeated operations.

## Input and report arguments

`samples/tasks.json` contains the project task data. Every task requires a unique nonblank ID, nonblank title, real date in `yyyy-MM-dd` form, importance 1–5, minutes 0–480 and a boolean completion flag. Missing, null, misspelled or invalid fields reject the whole dataset. A zero-minute reminder and `completed=false` are valid values.

Report arguments after `--reports` are:

```text
[file] [export.csv] [available minutes] [yyyy-MM-dd] [minimum importance]
```

Defaults are `samples/tasks.json`, `output/planner-report.csv`, 180 minutes, `2026-10-13` and minimum importance 3. Paths are relative to the working directory. Available time must be 1–480 and minimum importance 1–5. The chosen date must allow a following day for the workload window. Reports do not read the computer's clock.

## Report 1: daily focus

This report helps a student decide which eligible tasks to do first. It includes unfinished tasks on the chosen date whose importance meets the minimum. Output fields are position, ID, title, score and estimated minutes. The application runs the same dataset through both configured scoring functions.

For the default sample:

| Policy | IDs in order | Scores in that order | Total minutes |
| --- | --- | --- | --- |
| Importance first | T1, T2, T7, T3 | 50, 40, 40, 30 | 120 |
| Short tasks first | T3, T7, T2, T1 | 80, 80, 70, 50 | 120 |

`PlannerReports.DailyFocus` obtains eligible scores through the lazy workflow, materialises them with `ToArray`, orders by score descending and ID ascending, and projects them into `FocusRow` values with positions starting at 1. ID comparison is ordinal, so ties do not depend on the source order.

## Report 2: workload by date

This report helps a student compare time commitments. It includes all unfinished tasks from the selected date through the following date, inclusive. It intentionally includes lower-importance tasks that the focus report can exclude. Output fields are date, task count, total minutes and percentage of the daily time budget used.

`PlannerReports.WorkloadByDay` uses `Where`, `GroupBy`, `Select`, `Count`, `Sum`, `OrderByDescending` and `ThenBy`. Groups are ordered by total minutes descending, then date ascending. The native adapter adds the percentage after the pure grouping calculation.

| Date | Pending tasks | Minutes | Native load at 180 minutes |
| --- | --- | --- | --- |
| 2026-10-13 | 5 | 135 | 75% |
| 2026-10-14 | 1 | 20 | 12% |

The groups contain six tasks and 155 minutes in total. T5 is excluded because it is completed. T4 is included here even though its importance is below the daily-focus minimum. Workload above 100% is reported as overload; a report itself does not confirm that overloaded plan.

The CSV combines the two focus configurations and workload rows under this header:

```text
report,date,policy,position,id,title,tasks,minutes,score,load_percent
```

Text fields containing commas, quotes or line breaks are quoted. The workload `load_percent` field contains the actual native return value.

## Custom extensions

Both extensions are used by the report path:

| Extension | Receiver → return type | Purpose |
| --- | --- | --- |
| `EligibleOn(day, minimumImportance)` | `IEnumerable<TaskItem>` → `IEnumerable<TaskItem>` | Expresses the reusable combination of date, unfinished status and importance rules for a task sequence. |
| `RankForPlanning()` | `IEnumerable<ScoredTask>` → `IOrderedEnumerable<ScoredTask>` | Expresses the planner's score-descending, ordinal-ID tie ordering for scored task sequences. |

They belong on these sequence types because they operate on sets of tasks rather than one record. Neither changes the source collection. `EligibleOn` validates its threshold immediately and returns a deferred filter. `RankForPlanning` returns deferred ordering. `LazyPlanning` calls the first extension; `DailyFocus` calls the second.

## Currying and partial application

`CurriedScoring.Create` receives its inputs in successive calls:

```csharp
var withWeight = CurriedScoring.Create(10);
var importanceFirst = withWeight(0);
var shortTasksFirst = withWeight(60);
int score = shortTasksFirst(task);
```

Its shape is `weight -> bonusWindow -> TaskItem -> int`. Fixing weight and bonus window is partial application: it produces a function that only needs a task. The rule is:

```text
importance * weight + max(0, bonusWindow - minutes)
```

The readonly `ImportanceFirst` and `ShortTasksFirst` delegates use `(10, 0)` and `(10, 60)`. T3 has importance 3 and takes 10 minutes, so the same task scores 30 or 80. The application passes both delegates into `DailyFocus`; it does not switch a mutable global setting. Weight is 1–100 and the bonus window is 0–480 minutes. Invalid configuration is rejected before calculation.

## Deferred execution and materialisation

`LazyPlanning.ScoreEligible` returns an `IEnumerable<ScoredTask>`. Its iterator uses `yield return` to score actual eligible task records. Creating the sequence, or obtaining its enumerator, does not read or score its elements. `MoveNext` triggers processing.

Production calculations contain no diagnostic counters. Tests supply a counting policy and a probe enumerable to establish timing. They check no work at creation, expected work during partial iteration, repeated raw enumeration, and release of the source enumerator on early stop or an exception.

`DailyFocus` deliberately calls `ToArray` before ordering and projection. Enumerating the original lazy sequence again would repeat the calculation against its source; reading the materialised scores again does not rescore tasks. Returned reports are copied read-only snapshots.

## Resource ownership and retention

`TaskFile.Load` uses `File.ReadAllText`. That convenience API opens and closes its file internally, including on a read failure. `TaskParser` validates all records before publishing the resulting collection. Later lazy processing uses that in-memory snapshot, so it cannot consume a disposed reader or unexpectedly read a file again.

`ReportExporter.Save` owns the `FileStream` and `StreamWriter` it creates. Both are in `using` scopes. It writes a uniquely named temporary sibling of the destination, closes the writer and stream, then replaces the destination with `File.Move`. If writing or replacement fails, the boundary returns a Result failure and its `finally` block removes its own temporary file. A cleanup failure is reported rather than hidden. It refuses to export over the normalised input path.

Tests reopen a successful export with exclusive access, force a failed replacement, assert temporary-file cleanup and check that previous data remains unchanged. Deferred-source tests also assert disposal after an early stop and a processing exception. Garbage collection is not used as a file-release mechanism.

There is a memory trade-off: loading a snapshot uses memory proportional to the dataset, while a stream could process more data with less retained memory. This small planner chooses a snapshot so reports can reuse validated data without repeating I/O. A deferred query retains its source and scoring delegate while the query is referenced. The configured scoring closures capture only two integers. Materialised report rows hold their output fields, not the source collection. The application has no long-lived cache, event subscription or growing background collection. Temporary report arrays are local to a run and become collectible after that run's results are no longer referenced.

## Native C contract and binding

The project library exports:

```c
int32_t planner_load_percent(int32_t total_minutes, int32_t available_minutes);
```

| Contract item | Value |
| --- | --- |
| Valid total minutes | 0–1,000,000 |
| Valid available minutes | 1–480 |
| Result | Ceiling of `total_minutes * 100 / available_minutes`; zero is valid and results can exceed 100. |
| Invalid arguments | Returns `-1`. |
| Ownership | No allocation, returned pointer, retained state or native handle. |

For example, 120 minutes out of 180 is `ceil(12000 / 180) = 67`; 135 out of 180 is 75; 20 out of 180 rounds up to 12. The C implementation uses an `int64_t` intermediate and returns `int32_t`, avoiding assumptions about C `long` size.

`native/NativeMethods.cs` binds the exact export using `DllImport("planner_native")`, `CallingConvention.Cdecl`, `ExactSpelling=true` and C# `int` arguments/return value. `NativeLoadAdapter.Calculate` validates ranges before calling C and converts `DllNotFoundException`, `EntryPointNotFoundException` and `BadImageFormatException` into a clear Result failure with the library, operation and platform context. A negative native result is a failure. There is no managed substitute for a failed native call.

The real `--reports` workflow uses this adapter for each workload row. Tests call the actual library and compare its values with the examples above. Separate tests inject a throwing native call to exercise loading/binding failures. That controlled test does not replace the real-call test, and it avoids pretending that deleting an already-loaded DLL proves a fresh loading failure.

Reference: [Microsoft P/Invoke documentation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke).

## Rebuilding the native library

Windows x64 rebuild, from the project root with GCC on PATH:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File native/build.ps1
```

For the compiler used for this submission:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File native/build.ps1 -Compiler "C:\msys64\ucrt64\bin\gcc.exe"
```

The script verifies a Windows x64 GCC target and builds the DLL with:

```text
gcc -std=c11 -O2 -Wall -Wextra -Werror -shared -static-libgcc -Wl,--no-insert-timestamp native/planner_native.c -o runtimes/win-x64/native/planner_native.dll
```

Rebuild the .NET solution afterward so the updated native asset reaches both output folders. The supplied Windows library depends on Windows system libraries; it does not require an additional GCC runtime DLL.

For Khalid's Mac, install the matching .NET SDK and make a C compiler available through Apple's Command Line Tools. From the extracted project root:

```text
sh native/build.sh
dotnet build DailyPlanner.sln --disable-build-servers --maxcpucount:1
dotnet run --project Tests/DailyPlanner.Tests.csproj --no-build
```

The script uses `cc -dynamiclib` on macOS and selects `runtimes/osx-arm64/native/libplanner_native.dylib` or `runtimes/osx-x64/native/libplanner_native.dylib` from the machine architecture. Use a .NET SDK with the same architecture. It also provides a Linux `cc -fPIC -shared` build into the matching Linux runtime folder. **Windows x64 is the verified platform; macOS and Linux procedures are supplied but were not executed on those operating systems.**

An integration issue appeared because the existing project used an explicit list of C# source folders. The new native tests initially could not resolve `Tracker.Native` until `native/**/*.cs` was added to that list. Native binary placement is a separate concern: including the binding does not copy a DLL. `Directory.Build.targets` checks the runtime asset and explicitly copies it to both application and test output directories. Controlled loader-failure tests verify the reported error when a call cannot be loaded.

## Success and failure paths

`ReportWorkflow.Run` loads and validates data, calculates both pure reports, obtains native percentages, then exports the completed result. Its Bind chain stops after a failure; the final Map creates display text. `Main` handles the final outcome once. A failure prints an explanation to standard error and exits 1, without printing a partial successful report.

```text
dotnet run --project DailyPlanner.csproj --no-build -- --reports samples/not-found.json output/planner-report.csv
dotnet run --project DailyPlanner.csproj --no-build -- --reports samples/broken.json output/planner-report.csv
dotnet run --project DailyPlanner.csproj --no-build -- --reports samples/invalid.json output/planner-report.csv
dotnet run --project DailyPlanner.csproj --no-build -- --native-failure-demo samples/tasks.json output/planner-report.csv
```

The first three detect a missing file, malformed JSON and invalid task data. The last command deliberately injects a native loading failure for demonstration; it is not a real native-call success demonstration. All preserve an existing export. Normal `--reports` is the real native-call path.

Empty or nonmatching report data is valid: focus and workload rows are empty, and the exported CSV contains only its header. No native call is needed when there are no workload groups. Invalid report capacity is still rejected before processing empty input.

```text
dotnet run --project DailyPlanner.csproj --no-build -- --reports samples/empty.json output/empty.csv
dotnet run --project DailyPlanner.csproj --no-build -- --reports samples/tasks.json output/no-matches.csv 180 2026-10-20 3
```

Export failures return a clear failure and preserve earlier data. The report exporter does not save new task state or change existing task objects. The earlier planner still distinguishes an operation's Result from its Draft/Prepared/Confirmed lifecycle state; normal domain rejections do not depend on throwing and catching exceptions.

## Requirements and automated tests

The suite contains **111 cumulative tests**: 68 retained Assignment 7 tests and 43 new tests. The new tests extend these areas:

| Requirement | Implementation and tests |
| --- | --- |
| Two useful LINQ reports, filters, projections, ordering, grouping and aggregates | `PlannerReports.cs`; `ReportTests.cs` checks documented rows, empty/no-match data, both tie rules, grouping totals, date boundaries and snapshots. |
| Two used custom extensions | `TaskExtensions.cs`; `ExtensionTests.cs` checks eligibility, deterministic ranking and unchanged inputs. |
| Currying and two partial applications | `CurriedScoring.cs`; `ConfigurationTests.cs` checks the same task scoring 30/80, changed report order, bonus limits and invalid configuration. |
| Meaningful deferred workflow | `LazyPlanning.cs`; `DeferredTests.cs` checks timing, partial iteration, materialisation, reuse, early disposal and exception disposal. |
| Safe owned resources and export | `ReportExporter.cs`; `ExportTests.cs` checks exact UTF-8 output, released handles, replacement, failure cleanup, protected input and preserved earlier data. |
| Real native call and controlled failure | C source/header, `NativeMethods.cs`, `NativeLoadAdapter.cs`; `NativeTests.cs` checks real values, ranges, loader/binding errors and no fallback. |
| Complete report/native/export workflows | `ReportWorkflow.cs`; `ReportWorkflowTests.cs` checks integrated success and failure without partial updates. |
| Controlled file boundaries | Retained `FileBoundaryTests.cs` covers missing files and malformed content with temporary paths and cleanup. |
| Earlier pure calculations and safe states | Retained selection, scoring, plan, Result, Option, factory, rule and transition tests. |

The suite includes more than eight pure/domain tests, at least two complete workflow tests and controlled missing/malformed file tests. The assertion runner prints the actual total and pass count when run; packaging verification checks the same commands from a clean extraction.

## Team responsibilities

All members are in IT-2511. Each member reviews, tests and explains their stage files and the corresponding README sections.

| Member | Assignment 8 logic | Tests |
| --- | --- | --- |
| Yerkebulan Abenov | CLI, report workflow, CSV presentation and safe export | `ReportWorkflowTests.cs`, `ExportTests.cs` |
| Nurbergen Assan | Custom extensions and lazy scoring workflow | `ExtensionTests.cs`, `DeferredTests.cs` |
| Amanalina Tumargul | LINQ reports and curried scoring configurations | `ReportTests.cs`, `ConfigurationTests.cs` |
| Khalid Ahmad Ahmadi | C function, native build scripts, C# binding and adapter | `NativeTests.cs` |

For a small live change, adjust the focus threshold, change the bonus window, or change the workload tie order. Work out the expected rows first, update the matching assertions, rebuild and rerun the tests. Keep this integrated project for the final project.
