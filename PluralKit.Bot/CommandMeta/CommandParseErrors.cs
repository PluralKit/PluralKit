using Myriad.Types;

namespace PluralKit.Bot;

public partial class CommandTree
{
    private async Task PrintCommandList(Context ctx, string subject, string commands)
    {
        if (commands.Length == 0)
        {
            await ctx.Reply($"No commands related to `{subject}` was found. For the full list of commands, see the website: <https://pluralkit.me/commands>");
            return;
        }

        await ctx.Reply(
            components: [
                new MessageComponent()
                {
                    Type = ComponentType.Text,
                    Content = $"Here is a list of commands related to `{subject}`:\n{commands}\nFor a full list of possible commands, see <https://pluralkit.me/commands>.",
                }
            ]
        );
    }
}