using Myriad.Types;

namespace PluralKit.Bot;

public partial class CommandTree
{
    private async Task PrintCommandList(Context ctx, string subject, string commands)
    {
        await ctx.Reply(
            components: [
                new MessageComponent()
                {
                    Type = ComponentType.Text,
                    Content = $"Here is a list of commands related to {subject}:\n{commands}\nFor a full list of possible commands, see <https://pluralkit.me/commands>.",
                }
            ]
        );
    }
}