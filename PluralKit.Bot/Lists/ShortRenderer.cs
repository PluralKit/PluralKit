using System;
using System.Collections.Generic;
using System.Linq;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ShortRenderer: IListRenderer
    {
        public int MembersPerPage => 25;
        
        public void RenderPage(DiscordEmbedBuilder eb, PKSystem system, IEnumerable<PKListMember> members)
        {
            string RenderLine(PKListMember m)
            {
                if (m.HasProxyTags)
                {
                    var proxyTagsString = m.ProxyTagsString().SanitizeMentions();
                    if (proxyTagsString.Length > 100) // arbitrary threshold for now, tweak?
                        proxyTagsString = "tags too long, see member card";

                    return $"[`{m.Hid}`] **{m.Name.SanitizeMentions()}** *({proxyTagsString})*";
                }

                return $"[`{m.Hid}`] **{m.Name.SanitizeMentions()}**";
            }

            eb.Description = string.Join("\n", members.Select(RenderLine));
        }
    }
}