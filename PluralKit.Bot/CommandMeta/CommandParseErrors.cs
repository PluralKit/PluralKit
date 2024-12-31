using Humanizer;

using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public partial class CommandTree
{
    private async Task PrintCommandNotFoundError(Context ctx, params Command[] potentialCommands)
    {
        var commandListStr = CreatePotentialCommandList(ctx.DefaultPrefix, potentialCommands);
        await ctx.Reply(
            $"{Emojis.Error} Unknown command `{ctx.DefaultPrefix}{ctx.FullCommand().Truncate(100)}`. Perhaps you meant to use one of the following commands?\n{commandListStr}\n\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
    }

    private async Task PrintCommandExpectedError(Context ctx, params Command[] potentialCommands)
    {
        var commandListStr = CreatePotentialCommandList(ctx.DefaultPrefix, potentialCommands);
        await ctx.Reply(
            $"{Emojis.Error} You need to pass a command. Perhaps you meant to use one of the following commands?\n{commandListStr}\n\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
    }

    private static string CreatePotentialCommandList(string prefix, params Command[] potentialCommands)
    {
        return string.Join("\n", potentialCommands.Select(cmd => $"- **{prefix}{cmd.Usage}** - *{cmd.Description}*"));
    }

    private async Task PrintCommandList(Context ctx, string subject, params Command[] commands)
    {
        var str = CreatePotentialCommandList(ctx.DefaultPrefix, commands);
        await ctx.Reply(
            $"Here is a list of commands related to {subject}:",
            embed: new Embed()
            {
                Description = $"{str}\nFor a full list of possible commands, see <https://pluralkit.me/commands>.",
                Color = DiscordUtils.Blue,
            }
        );
    }

    private async Task<string> CreateSystemNotFoundError(Context ctx)
    {
        var input = ctx.PopArgument();
        if (input.TryParseMention(out var id))
        {
            // Try to resolve the user ID to find the associated account,
            // so we can print their username.
            var user = await ctx.Rest.GetUser(id);
            if (user != null)
                return $"Account **{user.Username}#{user.Discriminator}** does not have a system registered.";
            return $"Account with ID `{id}` not found.";
        }

        return $"System with ID {input.AsCode()} not found.";
    }
}