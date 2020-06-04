using System.Collections.Generic;

using DSharpPlus.Entities;

namespace PluralKit.Bot
{
    public interface IListRenderer
    {
        int MembersPerPage { get; }
        void RenderPage(DiscordEmbedBuilder eb, IEnumerable<PKListMember> members);
    }
}