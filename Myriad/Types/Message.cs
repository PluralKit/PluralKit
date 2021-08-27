using System;
using System.Text.Json.Serialization;

using Myriad.Utils;

namespace Myriad.Types
{
    public record Message
    {
        [Flags]
        public enum MessageFlags
        {
            Crossposted = 1 << 0,
            IsCrosspost = 1 << 1,
            SuppressEmbeds = 1 << 2,
            SourceMessageDeleted = 1 << 3,
            Urgent = 1 << 4,
            Ephemeral = 1 << 6
        }

        public enum MessageType
        {
            Default = 0,
            RecipientAdd = 1,
            RecipientRemove = 2,
            Call = 3,
            ChannelNameChange = 4,
            ChannelIconChange = 5,
            ChannelPinnedMessage = 6,
            GuildMemberJoin = 7,
            UserPremiumGuildSubscription = 8,
            UserPremiumGuildSubscriptionTier1 = 9,
            UserPremiumGuildSubscriptionTier2 = 10,
            UserPremiumGuildSubscriptionTier3 = 11,
            ChannelFollowAdd = 12,
            GuildDiscoveryDisqualified = 14,
            GuildDiscoveryRequalified = 15,
            Reply = 19,
            ApplicationCommand = 20,
            ThreadStarterMessage = 21,
            GuildInviteReminder = 22
        }

        public ulong Id { get; init; }
        public ulong ChannelId { get; init; }
        public ulong? GuildId { get; init; }
        public User Author { get; init; }
        public string? Content { get; init; }
        public string? Timestamp { get; init; }
        public string? EditedTimestamp { get; init; }
        public bool Tts { get; init; }
        public bool MentionEveryone { get; init; }
        public User.Extra[] Mentions { get; init; }
        public ulong[] MentionRoles { get; init; }

        public Attachment[] Attachments { get; init; }
        public Embed[]? Embeds { get; init; }
        public Reaction[] Reactions { get; init; }
        public bool Pinned { get; init; }
        public ulong? WebhookId { get; init; }
        public MessageType Type { get; init; }
        public Reference? MessageReference { get; set; }
        public MessageFlags Flags { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<Message?> ReferencedMessage { get; init; }
        public MessageComponent[]? Components { get; init; }

        public record Reference(ulong? GuildId, ulong? ChannelId, ulong? MessageId);

        public record Attachment
        {
            public ulong Id { get; init; }
            public string Filename { get; init; }
            public int Size { get; init; }
            public string Url { get; init; }
            public string ProxyUrl { get; init; }
            public int? Width { get; init; }
            public int? Height { get; init; }
        }

        public record Reaction
        {
            public int Count { get; init; }
            public bool Me { get; init; }
            public Emoji Emoji { get; init; }
        }
    }
}