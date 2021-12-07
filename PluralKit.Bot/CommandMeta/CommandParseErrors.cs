using Humanizer;

using PluralKit.Core;

namespace PluralKit.Bot;

public partial class CommandTree
{
    private async Task PrintCommandNotFoundError(Context ctx, params Command[] potentialCommands)
    {
        var commandListStr = CreatePotentialCommandList(potentialCommands);
        await ctx.Reply(
            $"{Emojis.Error} Unknown command `pk;{ctx.FullCommand().Truncate(100)}`. Perhaps you meant to use one of the following commands?\n{commandListStr}\n\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
    }
    private async Task PrintCommandExpectedError(Context ctx, params Command[] potentialCommands)
    {
        var commandListStr = CreatePotentialCommandList(potentialCommands);
        await ctx.Reply(
            $"{Emojis.Error} You need to pass a command. Perhaps you meant to use one of the following commands?\n{commandListStr}\n\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
    }

    private static string CreatePotentialCommandList(params Command[] potentialCommands)
    {
        return string.Join("\n", potentialCommands.Select(cmd => $"- **pk;{cmd.Usage}** - *{cmd.Description}*"));
    }

    private async Task PrintCommandList(Context ctx, string subject, params Command[] commands)
    {
        var str = CreatePotentialCommandList(commands);
        await ctx.Reply($"Here is a list of commands related to {subject}: \n{str}\nFor a full list of possible commands, see <https://pluralkit.me/commands>.");
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