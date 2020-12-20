#nullable enable
using NodaTime;

namespace PluralKit.Core
{
    public class SwitchPatch: PatchObject
    {
        public Partial<Instant> Timestamp { get; set; }
        public Partial<string?> Note { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("timestamp", Timestamp)
            .With("note", Note);

        protected bool Equals(SwitchPatch other)
        {
            return Timestamp.Equals(other.Timestamp) && Note.Equals(other.Note);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SwitchPatch) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Timestamp.GetHashCode() * 397) ^ Note.GetHashCode();
            }
        }

        public static bool operator ==(SwitchPatch? left, SwitchPatch? right) => Equals(left, right);

        public static bool operator !=(SwitchPatch? left, SwitchPatch? right) => !Equals(left, right);
    }
}