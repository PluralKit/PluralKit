namespace PluralKit.Core
{
    public readonly struct GroupId: INumericId<GroupId, int>
    {
        public int Value { get; }

        public GroupId(int value)
        {
            Value = value;
        }

        public bool Equals(GroupId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is GroupId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(GroupId left, GroupId right) => left.Equals(right);

        public static bool operator !=(GroupId left, GroupId right) => !left.Equals(right);

        public int CompareTo(GroupId other) => Value.CompareTo(other.Value);
        
        public override string ToString() => $"Group #{Value}";
    }
}