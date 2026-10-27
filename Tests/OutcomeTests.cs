using Tracker.Domain;

namespace Tracker.Tests;

internal static partial class Program
{
    private static IEnumerable<(string Name, Action Run)> OutcomeCases()
    {
        yield return ("Map receives the success payload once and transforms it", () =>
        {
            int calls = 0;
            var mapped = Result<int>.Ok(7).Map(number => { calls++; Equal(7, number); return number * 3; });
            Assert(mapped is Result<int>.Success { Value: 21 }, "Wrong mapped value.");
            Equal(1, calls);
        });
        yield return ("Bind receives success once and preserves the returned Result", () =>
        {
            int calls = 0;
            var next = Result<string>.Ok("ready");
            var bound = Result<int>.Ok(7).Bind(number => { calls++; Equal(7, number); return next; });
            Assert(ReferenceEquals(next, bound), "Bind changed the returned result.");
            Equal(1, calls);
        });
        yield return ("Bind preserves a business failure returned after success", () =>
        {
            var rejection = Result<int>.Fail("The requested capacity is too small.");
            var bound = Result<string>.Ok("T1").Bind(_ => rejection);
            Assert(ReferenceEquals(rejection, bound), "Bind lost the rejection.");
        });
        yield return ("Map and Bind preserve the original error without running callbacks", () =>
        {
            int maps = 0, binds = 0;
            var result = Result<int>.Fail("Task 'missing' was not found.")
                .Map(value => { maps++; return value * 2; })
                .Bind(value => { binds++; return Result<string>.Ok(value.ToString()); });
            Assert(result is Result<string>.Failure { Error: "Task 'missing' was not found." }, "Failure changed.");
            Equal(0, maps);
            Equal(0, binds);
        });
        yield return ("Result rejects missing success payloads and blank failure reasons", () =>
        {
            Throws<ArgumentNullException>(() => Result<string?>.Ok(null));
            Throws<ArgumentException>(() => Result<int>.Fail(" "));
            Throws<ArgumentNullException>(() => Result<int>.Fail(null));
            Throws<ArgumentNullException>(() => Result<string>.Ok("value").Map<string?>(_ => null));
        });

    }
}
