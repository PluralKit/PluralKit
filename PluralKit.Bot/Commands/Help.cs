using Myriad.Types;
using Myriad.Rest.Types.Requests;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Help
{
    private static Embed helpEmbed = new()
    {
        Title = "PluralKit",
        Description = "PluralKit is a bot designed for plural communities on Discord, and is open for anyone to use. It allows you to register systems, maintain system information, set up message proxying, log switches, and more.",
        Footer = new("By @ske | Myriad design by @layl, icon by @tedkalashnikov, banner by @fulmine | GitHub: https://github.com/PluralKit/PluralKit/ | Website: https://pluralkit.me/"),
        Color = DiscordUtils.Blue,
    };

    private static Dictionary<string, Embed.Field[]> helpEmbedPages = new Dictionary<string, Embed.Field[]>
    {
        {
            "default",
            new Embed.Field[]
            {
                new
                (
                    "System Recovery",
                    "In the case of your Discord account getting lost or deleted, the PluralKit staff can help you recover your system. "
                    + "In order to do so, we will need your **PluralKit token**. This is the *only* way you can prove ownership so we can help you recover your system. "
                    + "To get it, run `pk;token` and then store it in a safe place.\n\n"
                    + "Keep your token safe, if other people get access to it they can also use it to access your system. "
                    + "If your token is ever compromised run `pk;token refresh` to invalidate the old token and get a new one."
                ),
                new
                (
                    "Use the buttons below to see more info!",
                    ""
                )
            }
        },
        {
            "basicinfo",
            new Embed.Field[]
            {
                new
                (
                    "What is this for? What are systems?",
                    "This bot detects messages with certain tags associated with a profile, then replaces that message under a \"pseudo-account\" of that profile using webhooks."
                    + " This is useful for multiple people sharing one body (aka \"systems\"), people who wish to roleplay as different characters without having several accounts, or anyone else who may want to post messages as a different person from the same account."
                ),
                new
                (
                    "Why are people's names saying [APP] or [BOT] next to them?",
                    "These people are not actually apps or bots, this is just a Discord limitation. See [the documentation](https://pluralkit.me/guide#proxying) for an in-depth explanation."
                )
            }
        },
        {
            "gettingstarted",
            new Embed.Field[]
            {
                new
                (
                    "How do I get started?",
                    String.Join("\n", new[]
                    {
                        "To get started using PluralKit, try running the following commands (of course replacing the relevant names with your own):",
                        "**1**. `pk;system new` - Create a system (if you haven't already)",
                        "**2**. `pk;member add John` - Add a new member to your system",
                        "**3**. `pk;member John proxy [text]` - Set up [square brackets] as proxy tags",
                        "**4**. You're done! You can now type [a message in brackets] and it'll be proxied appropriately.",
                        "**5**. Optionally, you may set an avatar from the URL of an image with `pk;member John avatar [link to image]`, or from a file by typing `pk;member John avatar` and sending the message with an attached image.",
                        "\nSee [the Getting Started guide](https://pluralkit.me/start) for more information."
                    })
                ),
            }
        },
        {
            "usefultips",
            new Embed.Field[]
            {
                new
                (
                    "Useful tips",
                    String.Join("\n", new[] {
                        $"React with {Emojis.Error} on a proxied message to delete it (only if you sent it!)",
                        $"React with {Emojis.RedQuestion} on a proxied message to look up information about it (like who sent it)",
                        $"React with {Emojis.Bell} on a proxied message to \"ping\" the sender",
                        "Type **`pk;invite`** to get a link to invite this bot to your own server!"
                    })
                ),
            }
        },
        {
            "moreinfo",
            new Embed.Field[]
            {
                new
                (
                    "More information",
                    String.Join("\n", new[] {
                        "For a full list of commands, see [the command list](https://pluralkit.me/commands), or type `pk;commands`.",
                        "For a more in-depth explanation of message proxying, see [the documentation](https://pluralkit.me/guide#proxying).",
                        "If you're an existing user of Tupperbox, type `pk;import` and attach a Tupperbox export file (from `tul!export`) to import your data from there.",
                        "We also have a [web dashboard](https://dash.pluralkit.me) to edit your system info online."
                    })
                ),
                new
                (
                    "Support server",
                    "We also have a Discord server for support, discussion, suggestions, announcements, etc: https://discord.gg/PczBt78"
                ),
            }
        }
    };

    private static MessageComponent helpPageButtons(ulong userId) => new MessageComponent
    {
        Type = ComponentType.ActionRow,
        Components = new[]
        {
            new MessageComponent
            {
                Type = ComponentType.Button,
                Style = ButtonStyle.Secondary,
                Label = "Basic Info",
                CustomId = $"help-menu-basicinfo-{userId}",
                Emoji = new() { Name = "\u2139" },
            },
            new()
            {
                Type = ComponentType.Button,
                Style = ButtonStyle.Secondary,
                Label = "Getting Started",
                CustomId = $"help-menu-gettingstarted-{userId}",
                Emoji = new() { Name = "\u2753", },
            },
            new()
            {
                Type = ComponentType.Button,
                Style = ButtonStyle.Secondary,
                Label = "Useful Tips",
                CustomId = $"help-menu-usefultips-{userId}",
                Emoji = new() { Name = "\U0001f4a1", },

            },
            new()
            {
                Type = ComponentType.Button,
                Style = ButtonStyle.Secondary,
                Label = "More Info",
                CustomId = $"help-menu-moreinfo-{userId}",
                Emoji = new() { Id = 986379675066593330, },
            }
        }
    };

    public Task HelpRoot(Context ctx)
        => ctx.Rest.CreateMessage(ctx.Channel.Id, new MessageRequest
        {
            Content = $"{Emojis.Warn} If you cannot see the rest of this message see [the FAQ](<https://pluralkit.me/faq/#why-do-most-of-pluralkit-s-messages-look-blank-or-empty>)",
            Embeds = new[] { helpEmbed with { Description = helpEmbed.Description,
                                              Fields = helpEmbedPages.GetValueOrDefault("default") } },
            Components = new[] { helpPageButtons(ctx.Author.Id) },
        });

    public static Task ButtonClick(InteractionContext ctx)
    {
        if (!ctx.CustomId.Contains(ctx.User.Id.ToString()))
            return ctx.Ignore();

        var buttons = helpPageButtons(ctx.User.Id);

        if (ctx.Event.Message.Components.First().Components.Where(x => x.CustomId == ctx.CustomId).First().Style == ButtonStyle.Primary)
            return ctx.Respond(InteractionResponse.ResponseType.UpdateMessage, new()
            {
                Embeds = new[] { helpEmbed with { Fields = helpEmbedPages.GetValueOrDefault("default") } },
                Components = new[] { buttons }
            });

        buttons.Components.Where(x => x.CustomId == ctx.CustomId).First().Style = ButtonStyle.Primary;

        return ctx.Respond(InteractionResponse.ResponseType.UpdateMessage, new()
        {
            Embeds = new[] { helpEmbed with { Fields = helpEmbedPages.GetValueOrDefault(ctx.CustomId.Split("-")[2]) } },
            Components = new[] { buttons }
        });
    }

    private static string explanation = String.Join("\n\n", new[]
    {
        "> **About PluralKit**\nPluralKit detects messages enclosed in specific tags associated with a profile, then replaces that message under a \"pseudo-account\" of that profile using Discord webhooks.",
        "This is useful for multiple people sharing one body (aka. *systems*), people who wish to role-play as different characters without having multiple Discord accounts, or anyone else who may want to post messages under a different identity from the same Discord account.",
        "Due to Discord limitations, these messages will show up with the `[APP]` or `[BOT]` tag - however, they are not apps or bots."
    });

    public Task Explain(Context ctx) => ctx.Reply(explanation);

    public Task Dashboard(Context ctx) => ctx.Reply("The PluralKit dashboard is at <https://dash.pluralkit.me>");
}