using Myriad.Types;

namespace PluralKit.Bot;

public partial class CommandTree
{
    private async Task PrintCommandList(Context ctx, string subject, string commands)
    {
        await ctx.Reply(
            $"Here is a list of commands related to {subject}:",
            embed: new Embed()
            {
                Description = $"{commands}\nFor a full list of possible commands, see <https://pluralkit.me/commands>.",
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