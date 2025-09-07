using Myriad.Types;
using Myriad.Rest.Types.Requests;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Help
{
    public Task HelpRoot(Context ctx)
    {
        if (ctx.MatchFlag("show-embed", "se"))
            return HelpRootOld(ctx);

        return ctx.Reply(BuildComponents(ctx.Author.Id, Help.Description.Replace("{prefix}", ctx.DefaultPrefix), -1));
    }

    public static Task ButtonClick(InteractionContext ctx, string prefix)
    {
        if (!ctx.CustomId.Contains(ctx.User.Id.ToString()))
            return ctx.Ignore();

        if (ctx.CustomId.StartsWith("new-"))
        {
            Console.WriteLine($"{ctx.Event.Message.Components.First().Components.Length}");
            if (ctx.Event.Message.Components.First().Components[1].Components.Where(x => x.CustomId == ctx.CustomId).First().Style == ButtonStyle.Primary)
                return ctx.Respond(InteractionResponse.ResponseType.UpdateMessage, new()
                {
                    Components = BuildComponents(ctx.User.Id, Help.Description.Replace("{prefix}", prefix), -1),
                    Flags = Message.MessageFlags.IsComponentsV2,
                });

            var text = helpEmbedPages.GetValueOrDefault(ctx.CustomId.Split("-")[3]).Select(
                (item, index) => $"### {item.Name.Replace("{prefix}", prefix)}\n{item.Value.Replace("{prefix}", prefix)}"
            ).ToArray();

            var index = Array.FindIndex(ctx.Event.Message.Components.First().Components[1].Components, x => x.CustomId == ctx.CustomId);
            var components = BuildComponents(ctx.User.Id, Help.Description.Replace("{prefix}", prefix), index);

            components.First().Components[ctx.Event.Message.Components.First().Components.Length - 1] = new MessageComponent()
            {
                Type = ComponentType.Text,
                Content = String.Join("\n", text),
            };

            return ctx.Respond(InteractionResponse.ResponseType.UpdateMessage, new()
            {
                Components = components,
                Flags = Message.MessageFlags.IsComponentsV2,
            });
        }

        return ButtonClickOld(ctx, prefix);
    }

    private static MessageComponent[] BuildComponents(ulong userId, string textContent, int menuIndex)
    {
        return [
            new MessageComponent()
            {
                Type = ComponentType.Container,
                AccentColor = DiscordUtils.Blue,
                Components = [
                    new MessageComponent()
                    {
                        Type = ComponentType.Text,
                        Content = "# PluralKit\n-# Use the buttons below to see more info!"
                    },
                    helpPageButtons(userId, "new-", menuIndex),
                    new MessageComponent()
                    {
                        Type = ComponentType.Separator,
                    },
                    new MessageComponent()
                    {
                        Type = ComponentType.Text,
                        Content = textContent,
                    },
                ],
            },
            new MessageComponent()
            {
                Type = ComponentType.Text,
                Content = EmbedFooter("\n-# "),
            },
        ];
    }

    ///

    private static string Description = "PluralKit is a bot designed for plural communities on Discord, and is open for anyone to use. It allows you to register systems, maintain system information, set up message proxying, log switches, and more.\n\n" +
                "**System recovery:** in the case of your Discord account getting lost or deleted, the PluralKit staff can help you recover your system, **only if you save the system token from `{prefix}token`**. See [this FAQ entry](https://pluralkit.me/faq/#can-i-recover-my-system-if-i-lose-access-to-my-discord-account) for more details.\n\n" +
                "If PluralKit is useful to you, please consider donating on [Patreon](https://patreon.com/pluralkit) or [Buy Me A Coffee](https://buymeacoffee.com/pluralkit).";

    private static string DescriptionOld = $"{Description}\n## Use the buttons below to see more info!";

    public static string EmbedFooter(string linkSeparator) => $"-# PluralKit by @ske and contributors | Myriad design by @layl, icon by @tedkalashnikov, banner by @fulmine{linkSeparator}GitHub: https://github.com/PluralKit/PluralKit/ | Website: https://pluralkit.me/";

    public static Embed helpEmbed = new()
    {
        Title = "PluralKit",
        Color = DiscordUtils.Blue,
    };

    private static Dictionary<string, Embed.Field[]> helpEmbedPages = new Dictionary<string, Embed.Field[]>
    {
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
                        "**1**. `{prefix}system new` - Create a system (if you haven't already)",
                        "**2**. `{prefix}member add John` - Add a new member to your system",
                        "**3**. `{prefix}member John proxy [text]` - Set up [square brackets] as proxy tags",
                        "**4**. You're done! You can now type [a message in brackets] and it'll be proxied appropriately.",
                        "**5**. Optionally, you may set an avatar from the URL of an image with `{prefix}member John avatar [link to image]`, or from a file by typing `{prefix}member John avatar` and sending the message with an attached image.",
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
                        "Type **`{prefix}invite`** to get a link to invite this bot to your own server!"
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
                        "For a full list of commands, see [the command list](https://pluralkit.me/commands), or type `{prefix}commands`.",
                        "For a more in-depth explanation of message proxying, see [the documentation](https://pluralkit.me/guide#proxying).",
                        "If you're an existing user of Tupperbox, type `{prefix}import` and attach a Tupperbox export file (from `tul!export`) to import your data from there.",
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

    private static MessageComponent helpPageButtons(ulong userId, string pfx = "", int menuIndex = -1) => new MessageComponent
    {
        Type = ComponentType.ActionRow,
        Components = new[]
        {
            new MessageComponent
            {
                Type = ComponentType.Button,
                Style = menuIndex == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary,
                Label = "Basic Info",
                CustomId = $"{pfx}help-menu-basicinfo-{userId}",
                Emoji = new() { Name = "\u2139" },
            },
            new()
            {
                Type = ComponentType.Button,
                Style = menuIndex == 1 ? ButtonStyle.Primary : ButtonStyle.Secondary,
                Label = "Getting Started",
                CustomId = $"{pfx}help-menu-gettingstarted-{userId}",
                Emoji = new() { Name = "\u2753", },
            },
            new()
            {
                Type = ComponentType.Button,
                Style = menuIndex == 2 ? ButtonStyle.Primary : ButtonStyle.Secondary,
                Label = "Useful Tips",
                CustomId = $"{pfx}help-menu-usefultips-{userId}",
                Emoji = new() { Name = "\U0001f4a1", },
            },
            new()
            {
                Type = ComponentType.Button,
                Style = menuIndex == 3 ? ButtonStyle.Primary : ButtonStyle.Secondary,
                Label = "More Info",
                CustomId = $"{pfx}help-menu-moreinfo-{userId}",
                Emoji = new() { Id = 986379675066593330, },
            }
        }
    };

    public Task HelpRootOld(Context ctx)
        => ctx.Rest.CreateMessage(ctx.Channel.Id, new MessageRequest
        {
            Content = $"{Emojis.Warn} If you cannot see the rest of this message see [the FAQ](<https://pluralkit.me/faq/#why-do-most-of-pluralkit-s-messages-look-blank-or-empty>)",
            Embeds = new[] { helpEmbed with { Description = Help.DescriptionOld.Replace("{prefix}", ctx.DefaultPrefix), Fields = new Embed.Field[] { new("", EmbedFooter(" | ")) } } },
            Components = new[] { helpPageButtons(ctx.Author.Id) },
        });

    public static Task ButtonClickOld(InteractionContext ctx, string prefix)
    {
        var buttons = helpPageButtons(ctx.User.Id);

        if (ctx.Event.Message.Components.First().Components.Where(x => x.CustomId == ctx.CustomId).First().Style == ButtonStyle.Primary)
            return ctx.Respond(InteractionResponse.ResponseType.UpdateMessage, new()
            {
                Embeds = new[] { helpEmbed with { Description = Help.DescriptionOld.Replace("{prefix}", prefix), Fields = new Embed.Field[] { new("", EmbedFooter(" | ")) } } },
                Components = new[] { buttons }
            });

        buttons.Components.Where(x => x.CustomId == ctx.CustomId).First().Style = ButtonStyle.Primary;

        return ctx.Respond(InteractionResponse.ResponseType.UpdateMessage, new()
        {
            Embeds = new[] { helpEmbed with { Fields = helpEmbedPages.GetValueOrDefault(ctx.CustomId.Split("-")[2]).Select(
                (item, index) => new Embed.Field(item.Name.Replace("{prefix}", prefix), item.Value.Replace("{prefix}", prefix))
            ).Append(new("", EmbedFooter(" | "))).ToArray() } },
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