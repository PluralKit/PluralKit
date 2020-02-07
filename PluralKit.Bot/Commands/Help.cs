using System.Threading.Tasks;
using Discord;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class Help
    {
        public async Task HelpRoot(Context ctx)
        {
            await ctx.Reply(embed: new EmbedBuilder()
                .WithTitle("PluralKit")
                .WithDescription("PluralKit is a bot designed for plural communities on Discord. It allows you to register systems, maintain system information, set up message proxying, log switches, and more.")
                .AddField("What is this for? What are systems?", "This bot detects messages with certain tags associated with a profile, then replaces that message under a \"pseudo-account\" of that profile using webhooks. This is useful for multiple people sharing one body (aka \"systems\"), people who wish to roleplay as different characters without having several accounts, or anyone else who may want to post messages as a different person from the same account.")
                .AddField("Why are people's names saying [BOT] next to them?", "These people are not actually bots, this is just a Discord limitation. See [the documentation](https://pluralkit.me/guide#proxying) for an in-depth explanation.")
                .AddField("How do I get started?", "To get started with PluralKit, check out the [getting started page on the pluralkit website](https://pluralkit.me/start)!")
                .AddField("Useful tips", $"React with {Emojis.Error} on a proxied message to delete it (only if you sent it!)\nReact with {Emojis.RedQuestion} on a proxied message to look up information about it (like who sent it)\nReact with {Emojis.Bell} on a proxied message to \"ping\" the sender\nType **`pk;invite`** to get a link to invite this bot to your own server!")
                .AddField("More information", "For a full list of commands, see [the command list](https://pluralkit.me/commands).\nFor a more in-depth explanation of message proxying, see [the documentation](https://pluralkit.me/guide#proxying).\nIf you're an existing user of Tupperbox, type `pk;import` and attach a Tupperbox export file (from `tul!export`) to import your data from there.")
                .AddField("Support server", "We also have a Discord server for support, discussion, suggestions, announcements, etc: https://discord.gg/PczBt78")
                .WithFooter("By @Ske#6201 | GitHub: https://github.com/xSke/PluralKit/ | Website: https://pluralkit.me/")
                .WithColor(Color.Blue)
                .Build());
        }
    }
}