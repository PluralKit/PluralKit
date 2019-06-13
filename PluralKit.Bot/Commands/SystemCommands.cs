using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Discord.Commands;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Text;
using NodaTime.TimeZones;

namespace PluralKit.Bot.Commands
{
    [Group("system")]
    public class SystemCommands : ContextParameterModuleBase<PKSystem>
    {
        public override string Prefix => "system";
        public override string ContextNoun => "system";

        public SystemStore Systems {get; set;}
        public MemberStore Members {get; set;}
        public EmbedService EmbedService {get; set;}

        [Command]
        public async Task Query(PKSystem system = null) {
            if (system == null) system = Context.SenderSystem;
            if (system == null) throw Errors.NoSystemError;

            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateSystemEmbed(system));
        }

        [Command("new")]
        [Remarks("system new <name>")]
        public async Task New([Remainder] string systemName = null)
        {
            if (ContextEntity != null) throw Errors.NotOwnSystemError;
            if (Context.SenderSystem != null) throw Errors.ExistingSystemError;

            var system = await Systems.Create(systemName);
            await Systems.Link(system, Context.User.Id);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;help` for more information about commands you can use now.");
        }

        [Command("name")]
        [Remarks("system name <name>")]
        [MustHaveSystem]
        public async Task Name([Remainder] string newSystemName = null) {
            if (newSystemName != null && newSystemName.Length > Limits.MaxSystemNameLength) throw Errors.SystemNameTooLongError(newSystemName.Length);

            Context.SenderSystem.Name = newSystemName;
            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System name {(newSystemName != null ? "changed" : "cleared")}.");
        }

        [Command("description")]
        [Remarks("system description <description>")]
        [MustHaveSystem]
        public async Task Description([Remainder] string newDescription = null) {
            if (newDescription != null && newDescription.Length > Limits.MaxDescriptionLength) throw Errors.DescriptionTooLongError(newDescription.Length);

            Context.SenderSystem.Description = newDescription;
            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System description {(newDescription != null ? "changed" : "cleared")}.");
        }

        [Command("tag")]
        [Remarks("system tag <tag>")]
        [MustHaveSystem]
        public async Task Tag([Remainder] string newTag = null) {
            if (newTag.Length > Limits.MaxSystemTagLength) throw Errors.SystemNameTooLongError(newTag.Length);

            Context.SenderSystem.Tag = newTag;

            // Check unproxyable messages *after* changing the tag (so it's seen in the method) but *before* we save to DB (so we can cancel)
            var unproxyableMembers = await Members.GetUnproxyableMembers(Context.SenderSystem);
            if (unproxyableMembers.Count > 0) {
                var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} Changing your system tag to '{newTag}' will result in the following members being unproxyable, since the tag would bring their name over 32 characters:\n**{string.Join(", ", unproxyableMembers.Select((m) => m.Name))}**\nDo you want to continue anyway?");
                if (!await Context.PromptYesNo(msg)) throw new PKError("Tag change cancelled.");
            }

            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System tag {(newTag != null ? "changed" : "cleared")}.");
        }

        [Command("delete")]
        [Remarks("system delete")]
        [MustHaveSystem]
        public async Task Delete() {
            var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} Are you sure you want to delete your system? If so, reply to this message with your system's ID (`{Context.SenderSystem.Hid}`).\n**Note: this action is permanent.**");
            var reply = await Context.AwaitMessage(Context.Channel, Context.User, timeout: TimeSpan.FromMinutes(1));
            if (reply.Content != Context.SenderSystem.Hid) throw new PKError($"System deletion cancelled. Note that you must reply with your system ID (`{Context.SenderSystem.Hid}`) *verbatim*.");

            await Systems.Delete(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System deleted.");
        }

        [Group("list")]
        public class SystemListCommands: ModuleBase<PKCommandContext> {
            public MemberStore Members { get; set; }

            [Command]
            [Remarks("system [system] list")]
            public async Task MemberShortList() {
                var system = Context.GetContextEntity<PKSystem>() ?? Context.SenderSystem;
                if (system == null) throw Errors.NoSystemError;

                var members = await Members.GetBySystem(system);
                var embedTitle = system.Name != null ? $"Members of {system.Name} (`{system.Hid}`)" : $"Members of `{system.Hid}`";
                await Context.Paginate<PKMember>(
                    members.OrderBy(m => m.Name).ToList(),
                    25,
                    embedTitle,
                    (eb, ms) => eb.Description = string.Join("\n", ms.Select((m) => {
                        if (m.HasProxyTags) return $"[`{m.Hid}`] **{m.Name}** *({m.ProxyString})*";
                        return $"[`{m.Hid}`] **{m.Name}**";
                    }))
                );
            }

            [Command("full")]
            [Alias("big", "details", "long")]
            [Remarks("system [system] list full")]
            public async Task MemberLongList() {
                var system = Context.GetContextEntity<PKSystem>() ?? Context.SenderSystem;
                if (system == null) throw Errors.NoSystemError;

                var members = await Members.GetBySystem(system);
                var embedTitle = system.Name != null ? $"Members of {system.Name} (`{system.Hid}`)" : $"Members of `{system.Hid}`";
                await Context.Paginate<PKMember>(
                    members.OrderBy(m => m.Name).ToList(),
                    10,
                    embedTitle,
                    (eb, ms) => {
                        foreach (var m in ms) {
                            var profile = $"**ID**: {m.Hid}";
                            if (m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                            if (m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                            if (m.Prefix != null || m.Suffix != null) profile += $"\n**Proxy tags**: {m.ProxyString}";
                            if (m.Description != null) profile += $"\n\n{m.Description}";
                            eb.AddField(m.Name, profile);
                        }
                    }
                );
            }
        }

        [Command("timezone")]
        [Remarks("system timezone [timezone]")]
        public async Task SystemTimezone([Remainder] string zoneStr = null)
        {
            if (zoneStr == null)
            {
                Context.SenderSystem.UiTz = "UTC";
                await Systems.Save(Context.SenderSystem);
                await Context.Channel.SendMessageAsync($"{Emojis.Success} System time zone cleared.");
                return;
            }

            var zones = DateTimeZoneProviders.Tzdb;
            var zone = zones.GetZoneOrNull(zoneStr);
            if (zone == null) throw Errors.InvalidTimeZone(zoneStr);

            var currentTime = SystemClock.Instance.GetCurrentInstant().InZone(zone);
            var msg = await Context.Channel.SendMessageAsync(
                $"This will change the system time zone to {zone.Id}. The current time is {currentTime.ToString(Formats.DateTimeFormat, null)}. Is this correct?");
            if (!await Context.PromptYesNo(msg)) throw Errors.TimezoneChangeCancelled;
            Context.SenderSystem.UiTz = zone.Id;
            await Systems.Save(Context.SenderSystem);

            await Context.Channel.SendMessageAsync($"System time zone changed to {zone.Id}.");
        }

        public override async Task<PKSystem> ReadContextParameterAsync(string value)
        {
            var res = await new PKSystemTypeReader().ReadAsync(Context, value, _services);
            return res.IsSuccess ? res.BestMatch as PKSystem : null;
        }
    }
}