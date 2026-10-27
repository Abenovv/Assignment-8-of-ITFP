namespace Tracker.Domain;

public abstract class Option<T>
{
    private Option() { }

    public sealed class Some : Option<T>
    {
        public T Value { get; }
        internal Some(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }
    }

    public sealed class None : Option<T>
    {
        internal None() { }
    }

    public static Option<T> Present(T value) => new Some(value);
    public static Option<T> Missing() => new None();

    public Option<U> Map<U>(Func<T, U> transform) => this switch
    {
        Some some => Option<U>.Present(transform(some.Value)),
        None => Option<U>.Missing(),
        _ => throw new InvalidOperationException("Unknown optional value.")
    };
}
