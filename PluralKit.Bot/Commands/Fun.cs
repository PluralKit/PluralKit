using Myriad.Builders;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Fun
{
    public Task Mn(Context ctx) => ctx.Reply("Gotta catch 'em all!");

    public Task Fire(Context ctx) =>
        ctx.Reply("*Fire bursts from the fingers and a row of enemies take about 320 points of damage each.*");

    public Task Thunder(Context ctx) =>
        ctx.Reply("*The enemy is thunder struck for about 200 points of damage four separate times.*");

    public Task Freeze(Context ctx) =>
        ctx.Reply(
            "*Causes a very cold wind to swirl around one enemy, inflicting about 720 points of damage.*");

    public Task Starstorm(Context ctx) =>
        ctx.Reply("*The method of \"shaking off the stars\" which Poo learned in his training. It deals about 720 points of damage to each enemy.*");

    public Task Flash(Context ctx) =>
        ctx.Reply(
            "*It generates glorious rays that have a high probability of destroying all the enemies on the scene in a single strike.*");

    public Task Rool(Context ctx) =>
        ctx.Reply("*\"What the fuck is a Pokémon?\"*");

    public Task Sus(Context ctx) =>
        ctx.Reply("\U0001F4EE");

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
