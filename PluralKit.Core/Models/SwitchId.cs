namespace PluralKit.Core
{
    public readonly struct SwitchId: INumericId<SwitchId, int>
    {
        public int Value { get; }

        public SwitchId(int value)
        {
            Value = value;
        }

        public bool Equals(SwitchId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SwitchId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(SwitchId left, SwitchId right) => left.Equals(right);

        public static bool operator !=(SwitchId left, SwitchId right) => !left.Equals(right);

        public int CompareTo(SwitchId other) => Value.CompareTo(other.Value);
        
        public override string ToString() => $"Switch #{Value}";
    }
}