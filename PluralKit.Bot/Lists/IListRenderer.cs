using System.Collections.Generic;

using DSharpPlus.Entities;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public interface IListRenderer
    {
        int MembersPerPage { get; }
        void RenderPage(DiscordEmbedBuilder eb, DateTimeZone zone, IEnumerable<ListedMember> members, LookupContext ctx);
    }
}