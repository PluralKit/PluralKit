namespace PluralKit.Core
{
    public readonly struct SystemId: INumericId<SystemId, int>
    {
        public int Value { get; }

        public SystemId(int value)
        {
            Value = value;
        }

        public bool Equals(SystemId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SystemId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(SystemId left, SystemId right) => left.Equals(right);

        public static bool operator !=(SystemId left, SystemId right) => !left.Equals(right);

        public int CompareTo(SystemId other) => Value.CompareTo(other.Value);

        public override string ToString() => $"System #{Value}";
    }
}