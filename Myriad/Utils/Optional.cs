namespace Myriad.Utils
{
    public interface IOptional
    {
        object? GetValue();
    }

    public readonly struct Optional<T>: IOptional
    {
        public Optional(T value)
        {
            HasValue = true;
            Value = value;
        }

        public bool HasValue { get; }
        public object? GetValue() => Value;

        public T Value { get; }

        public static implicit operator Optional<T>(T value) => new(value);

        public static Optional<T> Some(T value) => new(value);
        public static Optional<T> None() => default;
    }
}