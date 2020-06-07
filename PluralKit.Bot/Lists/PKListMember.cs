using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class PKListMember: PKMember
    {
        public int MessageCount { get; set; }
        public ulong? LastMessage { get; set; }
        public Instant? LastSwitchTime { get; set; }
    }
}