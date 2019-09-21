using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using App.Metrics;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Humanizer;

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

            var commandsRun = Metrics.Snapshot.GetForContext("Bot").Meters.First(m => m.MultidimensionalName == BotMetrics.CommandsRun.Name).Value;

            DiscordSocketClient shard = Context.Channel is ITextChannel ? Context.Client.GetShardFor(Context.Guild) : null;
            var latencyStr = $"**Average:** {Context.Client.Latency}ms";
            if (shard != null)
                latencyStr += $"\n**Shard #{shard.ShardId}:** {shard.Latency}ms";

            await Context.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .AddField($"Connection Latency", latencyStr)
                .AddField("Messages processed", $"{messagesReceived.OneMinuteRate:F1}/s ({messagesReceived.FifteenMinuteRate:F1}/s over 15m)")
                .AddField("Messages proxied", $"{messagesProxied.OneMinuteRate:F1}/s ({messagesProxied.FifteenMinuteRate:F1}/s over 15m)")
                .AddField("Commands executed", $"{commandsRun.OneMinuteRate:F1}/s ({commandsRun.FifteenMinuteRate:F1}/s over 15m)")
                .Build());
        }

        [Command("permcheck")]
        [Summary("permcheck [guild]")]
        public async Task PermCheckGuild(ulong guildId)
        {
            // TODO: will this call break for sharding if you try to request a guild on a different bot instance?
            var guild = Context.Client.GetGuild(guildId) as IGuild;
            if (guild == null)
                throw Errors.GuildNotFound(guildId);

            var requiredPermissions = new []
            {
                ChannelPermission.ViewChannel, // Manage Messages automatically grants Send and Add Reactions, but not Read
                ChannelPermission.ManageMessages,
                ChannelPermission.ManageWebhooks
            };

            // Loop through every channel and group them by sets of permissions missing
            var permissionsMissing = new Dictionary<ulong, List<ITextChannel>>();
            foreach (var channel in await guild.GetTextChannelsAsync())
            {
                // TODO: do we need to hide channels here to prevent info-leaking?
                var perms = await channel.PermissionsIn();

                // We use a bitfield so we can set individual permission bits in the loop
                ulong missingPermissionField = 0;
                foreach (var requiredPermission in requiredPermissions)
                    if (!perms.Has(requiredPermission))
                        missingPermissionField |= (ulong) requiredPermission;

                // If we're not missing any permissions, don't bother adding it to the dict
                // This means we can check if the dict is empty to see if all channels are proxyable
                if (missingPermissionField != 0)
                {
                    permissionsMissing.TryAdd(missingPermissionField, new List<ITextChannel>());
                    permissionsMissing[missingPermissionField].Add(channel);
                }
            }

            // Generate the output embed
            var eb = new EmbedBuilder()
                .WithTitle($"Permission check for **{guild.Name}**");

            if (permissionsMissing.Count == 0)
            {
                eb.WithDescription($"No errors found, all channels proxyable :)").WithColor(Color.Green);
            }
            else
            {
                foreach (var (missingPermissionField, channels) in permissionsMissing)
                {
                    // Each missing permission field can have multiple missing channels
                    // so we extract them all and generate a comma-separated list
                    var missingPermissionNames = string.Join(", ", new ChannelPermissions(missingPermissionField)
                        .ToList()
                        .Select(perm => perm.Humanize().Transform(To.TitleCase)));

                    var channelsList = string.Join("\n", channels
                        .OrderBy(c => c.Position)
                        .Select(c => $"#{c.Name}"));
                    eb.AddField($"Missing *{missingPermissionNames}*", channelsList);
                    eb.WithColor(Color.Red);
                }
            }

            // Send! :)
            await Context.Channel.SendMessageAsync(embed: eb.Build());
        }

        [Command("permcheck")]
        [Summary("permcheck [guild]")]
        [RequireContext(ContextType.Guild, ErrorMessage =
            "When running this command in DMs, you must pass a guild ID.")]
        public Task PermCheckGuild() => PermCheckGuild(Context.Guild.Id);
    }
}