#nullable enable

using NodaTime;

namespace PluralKit.Core
{
    public class MemberPatch: PatchObject
    {
        public Partial<string> Name { get; set; }
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> AvatarUrl { get; set; }
        public Partial<string?> Color { get; set; }
        public Partial<LocalDate?> Birthday { get; set; }
        public Partial<string?> Pronouns { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<ProxyTag[]> ProxyTags { get; set; }
        public Partial<bool> KeepProxy { get; set; }
        public Partial<int> MessageCount { get; set; }
        public Partial<PrivacyLevel> Visibility { get; set; }
        public Partial<PrivacyLevel> NamePrivacy { get; set; }
        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> PronounPrivacy { get; set; }
        public Partial<PrivacyLevel> BirthdayPrivacy { get; set; }
        public Partial<PrivacyLevel> AvatarPrivacy { get; set; }
        public Partial<PrivacyLevel> MetadataPrivacy { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("display_name", DisplayName)
            .With("avatar_url", AvatarUrl)
            .With("color", Color)
            .With("birthday", Birthday)
            .With("pronouns", Pronouns)
            .With("description", Description)
            .With("proxy_tags", ProxyTags)
            .With("keep_proxy", KeepProxy)
            .With("message_count", MessageCount)
            .With("member_visibility", Visibility)
            .With("name_privacy", NamePrivacy)
            .With("description_privacy", DescriptionPrivacy)
            .With("pronoun_privacy", PronounPrivacy)
            .With("birthday_privacy", BirthdayPrivacy)
            .With("avatar_privacy", AvatarPrivacy)
            .With("metadata_privacy", MetadataPrivacy);

        protected bool Equals(MemberPatch other) => Name.Equals(other.Name) && DisplayName.Equals(other.DisplayName) && AvatarUrl.Equals(other.AvatarUrl) && Color.Equals(other.Color) && Birthday.Equals(other.Birthday) && Pronouns.Equals(other.Pronouns) && Description.Equals(other.Description) && ProxyTags.Equals(other.ProxyTags) && KeepProxy.Equals(other.KeepProxy) && MessageCount.Equals(other.MessageCount) && Visibility.Equals(other.Visibility) && NamePrivacy.Equals(other.NamePrivacy) && DescriptionPrivacy.Equals(other.DescriptionPrivacy) && PronounPrivacy.Equals(other.PronounPrivacy) && BirthdayPrivacy.Equals(other.BirthdayPrivacy) && AvatarPrivacy.Equals(other.AvatarPrivacy) && MetadataPrivacy.Equals(other.MetadataPrivacy);

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MemberPatch) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name.GetHashCode();
                hashCode = (hashCode * 397) ^ DisplayName.GetHashCode();
                hashCode = (hashCode * 397) ^ AvatarUrl.GetHashCode();
                hashCode = (hashCode * 397) ^ Color.GetHashCode();
                hashCode = (hashCode * 397) ^ Birthday.GetHashCode();
                hashCode = (hashCode * 397) ^ Pronouns.GetHashCode();
                hashCode = (hashCode * 397) ^ Description.GetHashCode();
                hashCode = (hashCode * 397) ^ ProxyTags.GetHashCode();
                hashCode = (hashCode * 397) ^ KeepProxy.GetHashCode();
                hashCode = (hashCode * 397) ^ MessageCount.GetHashCode();
                hashCode = (hashCode * 397) ^ Visibility.GetHashCode();
                hashCode = (hashCode * 397) ^ NamePrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ DescriptionPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ PronounPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ BirthdayPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ AvatarPrivacy.GetHashCode();
                hashCode = (hashCode * 397) ^ MetadataPrivacy.GetHashCode();
                return hashCode;
            }
        }
    }
}