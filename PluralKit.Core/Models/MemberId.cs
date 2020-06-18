namespace PluralKit.Core
{
    public readonly struct MemberId: INumericId<MemberId, int>
    {
        public int Value { get; }

        public MemberId(int value)
        {
            Value = value;
        }

        public bool Equals(MemberId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is MemberId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(MemberId left, MemberId right) => left.Equals(right);

        public static bool operator !=(MemberId left, MemberId right) => !left.Equals(right);

        public int CompareTo(MemberId other) => Value.CompareTo(other.Value);
        
        public override string ToString() => $"Member #{Value}";
    }
}