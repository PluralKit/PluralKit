using System.Linq;
using System.Threading.Tasks;
using App.Metrics;
using Discord;
using Discord.Commands;

namespace PluralKit.Bot.Commands {
    public class MiscCommands: ModuleBase<PKCommandContext> {
        public BotConfig BotConfig { get; set; }
        public IMetrics Metrics { get; set; }
        
        [Command("invite")]
        [Alias("inv")]
        [Remarks("invite")]
        public async Task Invite()
        {
            var clientId = BotConfig.ClientId ?? (await Context.Client.GetApplicationInfoAsync()).Id;
            var permissions = new GuildPermissions(
                addReactions: true,
                attachFiles: true,
                embedLinks: true,
                manageMessages: true,
                manageWebhooks: true,
                readMessageHistory: true,
                sendMessages: true
            );

            var invite = $"https://discordapp.com/oauth2/authorize?client_id={clientId}&scope=bot&permissions={permissions.RawValue}";
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Use this link to add PluralKit to your server:\n<{invite}>");
        }

        [Command("mn")] public Task Mn() => Context.Channel.SendMessageAsync("Gotta catch 'em all!");
        [Command("fire")] public Task Fire() => Context.Channel.SendMessageAsync("*A giant lightning bolt promptly erupts into a pillar of fire as it hits your opponent.*");
        [Command("thunder")] public Task Thunder() => Context.Channel.SendMessageAsync("*A giant ball of lightning is conjured and fired directly at your opponent, vanquishing them.*");
        [Command("freeze")] public Task Freeze() => Context.Channel.SendMessageAsync("*A giant crystal ball of ice is charged and hurled toward your opponent, bursting open and freezing them solid on contact.*");
        [Command("starstorm")] public Task Starstorm() => Context.Channel.SendMessageAsync("*Vibrant colours burst forth from the sky as meteors rain down upon your opponent.*");

        [Command("stats")]
        public async Task Stats()
        {
            var messagesReceived = Metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.MessagesReceived.Name).Value;
            var messagesProxied = Metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.MessagesProxied.Name).Value;
            var proxySuccessRate = messagesProxied.Items.First(i => i.Item == "success");
            
            var commandsRun = Metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.CommandsRun.Name).Value;
            
            await Context.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .AddField("Messages processed", $"{messagesReceived.OneMinuteRate:F1}/s ({messagesReceived.FifteenMinuteRate:F1}/s over 15m)")
                .AddField("Messages proxied", $"{messagesProxied.OneMinuteRate:F1}/s ({messagesProxied.FifteenMinuteRate:F1}/s over 15m)")
                .AddField("Commands executed", $"{commandsRun.OneMinuteRate:F1}/s ({commandsRun.FifteenMinuteRate:F1}/s over 15m)")
                .AddField("Proxy success rate", $"{proxySuccessRate.Percent/100:P1}")
                .Build());
        }
    }
}