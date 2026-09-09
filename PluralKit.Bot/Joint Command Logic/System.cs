using PluralKit.Core;

namespace PluralKit.Bot;

using Myriad.Builders;
using Myriad.Types;

public class SystemLogic
{

    private readonly ModelRepository _repo;

    public SystemLogic(ModelRepository repo)
    {
        _repo = repo;
    }

    public async Task<MessageComponent> New(ulong authorId, string prefix, string? systemName = null, bool slashcommand = false, bool legacyEmbed = false)
    {
        if (systemName != null && systemName.Length > Limits.MaxSystemNameLength)
            throw Errors.StringTooLongError("System name", systemName.Length, Limits.MaxSystemNameLength);

        var system = await _repo.CreateSystem(systemName);
        await _repo.AddAccount(system.Id, authorId);

        if (legacyEmbed)
        {
            //TODO figure out how to return this
            // using out var doesn't work on async methods so that's not a solution
            var eb = new EmbedBuilder()
                .Title(
                    $"{Emojis.Success} Your system has been created.")
                .Field(new Embed.Field("Getting Started",
                    "New to PK? Check out our Getting Started guide on setting up members and proxies: https://pluralkit.me/start\n" +
                    $"Otherwise, type `{prefix}system` to view your system and `{prefix}system help` for more information about commands you can use."))
                .Field(new Embed.Field($"{Emojis.Warn} Notice: Public By Default {Emojis.Warn}", "PluralKit is a bot meant to help you share information about your system. " +
                    "Member descriptions are meant to be the equivalent to a Discord About Me. Because of this, any info you put in PK is **public by default**.\n" +
                    "Note that this does **not** include message content, only member fields. For more information, check out " +
                    "[the privacy section of the user guide](https://pluralkit.me/guide/#privacy). "))
                .Field(new Embed.Field($"{Emojis.Warn} Notice: Implicit Acceptance of ToS {Emojis.Warn}", "By using the PluralKit bot you implicitly agree to our " +
                    "[Terms of Service](https://pluralkit.me/terms-of-service/). For questions please ask in our [support server](<https://discord.gg/PczBt78>) or " +
                    "email legal@pluralkit.me"))
                .Field(new Embed.Field("System Recovery", "In the case of your Discord account getting lost or deleted, the PluralKit staff can help you recover your system. " +
                    "In order to do so, we will need your **PluralKit token**. This is the *only* way you can prove ownership so we can help you recover your system. " +
                    $"To get it, run `{prefix}token` and then store it in a safe place.\n\n" +
                    "Keep your token safe, if other people get access to it they can also use it to access your system. " +
                    $"If your token is ever compromised run `{prefix}token refresh` to invalidate the old token and get a new one."))
                .Field(new Embed.Field("Questions?",
                    "Please join the PK server https://discord.gg/PczBt78 if you have any questions, we're happy to help"));
            // await ctx.Reply($"{Emojis.Warn} If you cannot see the rest of this message see [the FAQ](<https://pluralkit.me/faq/#why-do-most-of-pluralkit-s-messages-look-blank-or-empty>)", eb.Build());
        }

        return new MessageComponent()
        {
            Type = ComponentType.Container,
            Components = [
                new ()
                {
                    Type = ComponentType.Text,
                    Content = $"# {Emojis.Success} Your system has been created{ (systemName != null ? $" with name \"{systemName}\"" : "") }."
                },
                new ()
                {
                    Type = ComponentType.Text,
                    Content = "## Getting Started\n" +
                        "New to PK? Check out our Getting Started guide on setting up members and proxies: https://pluralkit.me/start\n" +
                        $"Otherwise, type `{prefix}system{ (slashcommand ? " show" : "") }` to view your system and `{prefix}system help` " +
                        "for more information about commands you can use."
                },
                new ()
                {
                    Type = ComponentType.Text,
                    Content = $"## {Emojis.Warn} Notice: Public By Default {Emojis.Warn}\n" +
                        "PluralKit is a bot meant to help you share information about your system. Member descriptions are meant to be the equivalent to a Discord About Me. " +
                        "Because of this, any info you put in PK is **public by default**.\n" +
                        "Note that this does **not** include message content, only member fields. For more information, check out " +
                        "[the privacy section of the user guide](https://pluralkit.me/guide/#privacy). "
                },
                new ()
                {
                    Type = ComponentType.Text,
                    Content = $"## {Emojis.Warn} Notice: Implicit Acceptance of ToS {Emojis.Warn}\n" +
                        "By using the PluralKit bot you implicitly agree to our [Terms of Service](https://pluralkit.me/terms-of-service/). " +
                        "For questions please ask in our [support server](<https://discord.gg/PczBt78>) or email legal@pluralkit.me"
                },
                new ()
                {
                    Type = ComponentType.Text,
                    Content = "## System Recovery\n" +
                    "In the case of your Discord account getting lost or deleted, the PluralKit staff can help you recover your system. " +
                    "In order to do so, we will need your **PluralKit token**. This is the *only* way you can prove ownership so we can help you recover your system. " +
                    $"To get it, run `{prefix}token{ (slashcommand ? " get" : "") }` and then store it in a safe place.\n\n" +
                    "Keep your token safe, if other people get access to it they can also use it to access your system. " +
                    $"If your token is ever compromised run `{prefix}token refresh` to invalidate the old token and get a new one."
                },
                new ()
                {
                    Type = ComponentType.Text,
                    Content = "## Questions?\n" +
                        "Please join the PK server https://discord.gg/PczBt78 if you have any questions, we're happy to help"
                }
            ]
        };

    }
}