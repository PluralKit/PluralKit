#nullable enable
namespace PluralKit.Core
{
    public class SystemPatch: PatchObject
    {
        public Partial<string?> Name { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Tag { get; set; }
        public Partial<string?> AvatarUrl { get; set; }
        public Partial<string?> Token { get; set; }
        public Partial<string> UiTz { get; set; }
        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> MemberListPrivacy { get; set; }
        public Partial<PrivacyLevel> GroupListPrivacy { get; set; }
        public Partial<PrivacyLevel> FrontPrivacy { get; set; }
        public Partial<PrivacyLevel> FrontHistoryPrivacy { get; set; }
        public Partial<bool> PingsEnabled { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("description", Description)
            .With("tag", Tag)
            .With("avatar_url", AvatarUrl)
            .With("token", Token)
            .With("ui_tz", UiTz)
            .With("description_privacy", DescriptionPrivacy)
            .With("member_list_privacy", MemberListPrivacy)
            .With("group_list_privacy", GroupListPrivacy)
            .With("front_privacy", FrontPrivacy)
            .With("front_history_privacy", FrontHistoryPrivacy)
            .With("pings_enabled", PingsEnabled);

        protected bool Equals(SystemPatch other) => Name.Equals(other.Name) && Description.Equals(other.Description) && Tag.Equals(other.Tag) && AvatarUrl.Equals(other.AvatarUrl) && Token.Equals(other.Token) && UiTz.Equals(other.UiTz) && DescriptionPrivacy.Equals(other.DescriptionPrivacy) && MemberListPrivacy.Equals(other.MemberListPrivacy) && GroupListPrivacy.Equals(other.GroupListPrivacy) && FrontPrivacy.Equals(other.FrontPrivacy) && FrontHistoryPrivacy.Equals(other.FrontHistoryPrivacy) && PingsEnabled.Equals(other.PingsEnabled);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SystemPatch) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name.GetHashCode();
                hashCode = (hashCode * 397) ^ Description.GetHashCode();
                hashCode = (hashCode * 397) ^ Tag.GetHashCode();
                hashCode = (hashCode * 397) ^ AvatarUrl.GetHashCode();
                hashCode = (hashCode * 397) ^ Token.GetHashCode();
                hashCode = (hashCode * 397) ^ UiTz.GetHashCode();
                hashCode = (hashCode * 397) ^ DescriptionPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ MemberListPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ GroupListPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ FrontPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ FrontHistoryPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ PingsEnabled.GetHashCode();
                return hashCode;
            }
        }
    }
}