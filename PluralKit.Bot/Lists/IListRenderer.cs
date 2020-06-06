using System.Collections.Generic;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public interface IListRenderer
    {
        int MembersPerPage { get; }
        void RenderPage(DiscordEmbedBuilder eb, PKSystem system, IEnumerable<PKListMember> members);
    }
}