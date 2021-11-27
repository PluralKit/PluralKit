using Myriad.Builders;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Fun
{
    public Task Mn(Context ctx) => ctx.Reply("Gotta catch 'em all!");

    public Task Fire(Context ctx) =>
        ctx.Reply("*A giant lightning bolt promptly erupts into a pillar of fire as it hits your opponent.*");

    public Task Thunder(Context ctx) =>
        ctx.Reply("*A giant ball of lightning is conjured and fired directly at your opponent, vanquishing them.*");

    public Task Freeze(Context ctx) =>
        ctx.Reply(
            "*A giant crystal ball of ice is charged and hurled toward your opponent, bursting open and freezing them solid on contact.*");

    public Task Starstorm(Context ctx) =>
        ctx.Reply("*Vibrant colours burst forth from the sky as meteors rain down upon your opponent.*");

    public Task Flash(Context ctx) =>
        ctx.Reply(
            "*A ball of green light appears above your head and flies towards your enemy, exploding on contact.*");

    public Task Error(Context ctx)
    {
        if (ctx.Match("message"))
            return ctx.Reply("> **Error code:** `50f3c7b439d111ecab2023a5431fffbd`", new EmbedBuilder()
                .Color(0xE74C3C)
                .Title("Internal error occurred")
                .Description(
                    "For support, please send the error code above in **#bug-reports-and-errors** on **[the support server *(click to join)*](https://discord.gg/PczBt78)** with a description of what you were doing at the time.")
                .Footer(new Embed.EmbedFooter("50f3c7b439d111ecab2023a5431fffbd"))
                .Timestamp(SystemClock.Instance.GetCurrentInstant().ToDateTimeOffset().ToString("O"))
                .Build()
            );

        return ctx.Reply(
            $"{Emojis.Error} Unknown command {"error".AsCode()}. For a list of possible commands, see <https://pluralkit.me/commands>.");
    }
}