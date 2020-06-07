using System.Collections.Generic;
using System.Text;

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

            var buf = new StringBuilder();
            var chunks = new List<string>();
            
            // Split the list into properly-sized chunks
            foreach (var m in members)
            {
                var line = RenderLine(m);
                
                // First chunk goes in description (2048 chars), rest go in embed values (1000 chars)
                var lengthLimit = chunks.Count == 0 ? 2048 : 1000;
                if (buf.Length + line.Length + 1 > lengthLimit)
                {
                    chunks.Add(buf.ToString());
                    buf.Clear();
                }

                buf.Append(RenderLine(m));
                buf.Append("\n");
            }
            chunks.Add(buf.ToString());

            // Put the first chunk in the description, rest in blank-name embed fields
            eb.Description = chunks[0];
            for (var i = 1; i < chunks.Count; i++)
                // Field name is Unicode zero-width space
                eb.AddField("\u200B", chunks[i]);
        }
    }
}