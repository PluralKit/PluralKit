using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Linq;

using Dapper;

using Myriad.Extensions;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types.Requests;
using Myriad.Rest.Types;
using Myriad.Types;
using Myriad.Builders;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Member
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly EmbedService _embeds;
        private readonly HttpClient _client;
        private readonly Bot _bot;
        
        public Member(EmbedService embeds, IDatabase db, ModelRepository repo, HttpClient client, Bot bot)
        {
            _embeds = embeds;
            _db = db;
            _repo = repo;
            _client = client;
            _bot = bot;
        }

        public async Task NewMember(Context ctx) {
            if (ctx.System == null) throw Errors.NoSystemError;
            var memberName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a member name.");
            
            // Hard name length cap
            if (memberName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(memberName.Length);

            // Warn if there's already a member by this name
            var existingMember = await _db.Execute(c => _repo.GetMemberByName(c, ctx.System.Id, memberName));
            if (existingMember != null) {
                var msg = $"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.NameFor(ctx)}\" (with ID `{existingMember.Hid}`). Do you want to create another member with the same name?";
                if (!await ctx.PromptYesNo(msg, "Create")) throw new PKError("Member creation cancelled.");
            }

            await using var conn = await _db.Obtain();

            // Enforce per-system member limit
            var memberCount = await _repo.GetSystemMemberCount(conn, ctx.System.Id);
            var memberLimit = ctx.System.MemberLimitOverride ?? Limits.MaxMemberCount;
            if (memberCount >= memberLimit)
                throw Errors.MemberLimitReachedError(memberLimit);

            // Create the member
            var member = await _repo.CreateMember(conn, ctx.System.Id, memberName);
            memberCount++;

            // Try to match an image attached to the message
            var avatarArg = ctx.Message.Attachments.FirstOrDefault();
            Exception imageMatchError = null;
            if (avatarArg != null)
            {
                try {
                    await AvatarUtils.VerifyAvatarOrThrow(avatarArg.Url);
                    await _db.Execute(conn => _repo.UpdateMember(conn, member.Id, new MemberPatch { AvatarUrl = avatarArg.Url }));
                } catch (Exception e) {
                    imageMatchError = e;
                }
            }

            // Send confirmation and space hint
            var text = $"{Emojis.Success} Member \"{memberName}\" (`{member.Hid}`) registered! Check out the getting started page for how to get a member up and running: https://pluralkit.me/start#create-a-member";

            if (await _db.Execute(conn => conn.QuerySingleAsync<bool>("select has_private_members(@System)",
                new {System = ctx.System.Id}))) //if has private members
                text += $"\n{Emojis.Warn} This member is currently **public**. To change this, use `pk;member {member.Hid} private`.";
            if (avatarArg != null)
                if (imageMatchError == null)
                    text += $"\n{Emojis.Success} Member avatar set to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the avatar will stop working.";
                else
                    text += $"\n{Emojis.Error} Couldn't set avatar: {imageMatchError.Message}";
            if (memberName.Contains(" "))
                text += $"\n{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's 5-character ID (which is `{member.Hid}`).";
            if (memberCount >= memberLimit)
                text += $"\n{Emojis.Warn} You have reached the per-system member limit ({memberLimit}). You will be unable to create additional members until existing members are deleted.";
            else if (memberCount >= Limits.MaxMembersWarnThreshold(memberLimit))
                text += $"\n{Emojis.Warn} You are approaching the per-system member limit ({memberCount} / {memberLimit} members). Please review your member list for unused or duplicate members.";

            Embed? embed = null;
            if (ctx.System.WelcomeMessage != null) {
                var eb = new EmbedBuilder()
                    .Title($"Welcome, {memberName}!")
                    .Color(ctx.System.Color?.ToDiscordColor() ?? DiscordUtils.Gray)
                    .Description(ctx.System.WelcomeMessage)
                    .Footer(new($"System ID: {ctx.System.Hid}"));

                switch (ctx.System.WelcomeMessageMode) {
                    case WelcomeMessageMode.Off: break;
                    case WelcomeMessageMode.Inline:
                    {
                        embed = eb.Build();
                        break;
                    }
                    case WelcomeMessageMode.DM:
                    {
                        try {
                            var dm = await ctx.Cache.GetOrCreateDmChannel(ctx.Rest, ctx.Author.Id);
                            await ctx.Rest.CreateMessage(dm.Id, new MessageRequest{
                                Embed = eb.Build()
                            });
                        }
                        catch (ForbiddenException)
                        {
                            if (ctx.Channel.Type != Channel.ChannelType.Dm)
                                text += $"\n{Emojis.Error} Could not send welcome message in DMs. Are your DMs closed?";
                        }
                        break;
                    }
                    case WelcomeMessageMode.CustomChannel:
                    {
                        // This shouldn't ever happen, but if it does, act as if there's no message set at all.
                        if (ctx.System.WelcomeMessageChannel == null) break;

                        var channel = await ctx.Cache.GetOrFetchChannel(ctx.Rest, ctx.System.WelcomeMessageChannel.Value);
                        if (channel == null)
                        {
                            text += $"\n{Emojis.Error} Could not find your welcome channel, has it been deleted?";
                            break;
                        }

                        // GuildId should never be null
                        if (channel.GuildId == null) {
                            text += $"\n{Emojis.Error} Your welcome channel isn't in a server.";
                            break;
                        }

                        var guildMember = await ctx.Rest.GetGuildMember(channel.GuildId.Value, ctx.Author.Id);
                        if (guildMember == null) {
                            text += $"\n{Emojis.Error} You're not in the server your welcome channel is in.";
                            break;
                        }

                        var perms = ctx.Cache.PermissionsFor(channel.Id, guildMember);

                        if (!perms.HasFlag(PermissionSet.ViewChannel | PermissionSet.ReadMessageHistory))
                        {
                            text += $"\n{Emojis.Error} Could not find welcome channel, you are not in the server the welcome channel is in or you do not have permissions to view the welcome channel.";
                            break;
                        }
                        if (!perms.HasFlag(PermissionSet.SendMessages))
                        {
                            text += $"\n{Emojis.Error} You can't send messages in your welcome channel.";
                            break;
                        }

                        var botPerms = _bot.PermissionsIn(channel.Id);
                        if (!botPerms.HasFlag(PermissionSet.ViewChannel) || !botPerms.HasFlag(PermissionSet.SendMessages)) {
                            text += $"\n{Emojis.Error} PluralKit does not have permission to send messages in your welcome channel.";
                            break;
                        }

                        await ctx.Rest.CreateMessage(channel.Id, new MessageRequest{
                            Content = ctx.Author.Mention(),
                            Embed = eb.Build(),
                            AllowedMentions = new AllowedMentions {
                                Users = new ulong[] { ctx.Author.Id }
                            }
                        });
                        break;
                    }

                    default: throw new ArgumentOutOfRangeException();
                }
            }

            await ctx.Reply(text, embed: embed);
        }
        
        public async Task ViewMember(Context ctx, PKMember target)
        {
            var system = await _db.Execute(c => _repo.GetSystem(c, target.System));
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, target, ctx.Guild, ctx.LookupContextFor(system)));
        }

        public async Task Soulscream(Context ctx, PKMember target)
        {
            // this is for a meme, please don't take this code seriously. :)
            
            var name = target.NameFor(ctx.LookupContextFor(target));
            var encoded = HttpUtility.UrlEncode(name);
            
            var resp = await _client.GetAsync($"https://onomancer.sibr.dev/api/generateStats2?name={encoded}");
            if (resp.StatusCode != HttpStatusCode.OK)
                // lol
                return;

            var data = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var scream = data["soulscream"]!.Value<string>();

            var eb = new EmbedBuilder()
                .Color(DiscordUtils.Red)
                .Title(name)
                .Url($"https://onomancer.sibr.dev/reflect?name={encoded}")
                .Description($"*{scream}*");
            await ctx.Reply(embed: eb.Build());
        }
    }
}
