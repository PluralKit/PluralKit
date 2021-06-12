using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Linq;

using Dapper;

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
        
        public Member(EmbedService embeds, IDatabase db, ModelRepository repo, HttpClient client)
        {
            _embeds = embeds;
            _db = db;
            _repo = repo;
            _client = client;
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
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member creation cancelled.");
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
            await ctx.Reply($"{Emojis.Success} Member \"{memberName}\" (`{member.Hid}`) registered! Check out the getting started page for how to get a member up and running: https://pluralkit.me/start#create-a-member");
            if (await _db.Execute(conn => conn.QuerySingleAsync<bool>("select has_private_members(@System)",
                new {System = ctx.System.Id}))) //if has private members
                await ctx.Reply($"{Emojis.Warn} This member is currently **public**. To change this, use `pk;member {member.Hid} private`.");
            if (avatarArg != null)
                if (imageMatchError == null)
                    await ctx.Reply($"{Emojis.Success} Member avatar set to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the avatar will stop working.");
                else
                    await ctx.Reply($"{Emojis.Error} Couldn't set avatar: {imageMatchError.Message}");
            if (memberName.Contains(" "))
                await ctx.Reply($"{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's 5-character ID (which is `{member.Hid}`).");
            if (memberCount >= memberLimit)
                await ctx.Reply($"{Emojis.Warn} You have reached the per-system member limit ({memberLimit}). You will be unable to create additional members until existing members are deleted.");
            else if (memberCount >= Limits.MaxMembersWarnThreshold(memberLimit))
                await ctx.Reply($"{Emojis.Warn} You are approaching the per-system member limit ({memberCount} / {memberLimit} members). Please review your member list for unused or duplicate members.");
        }
        
        public async Task ViewMember(Context ctx, PKMember target)
        {
            var system = await _db.Execute(c => _repo.GetSystem(c, target.System));
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, target, ctx.Guild, ctx.LookupContextFor(system)));
        }
        public async Task AddReminder(Context ctx, PKMember target) {
            if (ctx.System?.Id == target.System) {
                await using var conn = await _db.Obtain();
                await _repo.AddReminder(
                    conn, 
                    new PKReminder { 
                        Mid = ctx.Message.Id, 
                        Channel = ctx.Channel.Id, 
                        Guild = ctx.Guild == null ? null : ctx.Guild.Id, 
                        Member = target.Id, 
                        System = target.System });
                await ctx.Reply($"Added new reminder for {target.Name}");
            }
            else {
                await ctx.Reply($"{Emojis.Error} You can only send reminders to your own system.");
            }
        }

        public async Task GetReminders(Context ctx, PKMember target) {
            if (ctx.System?.Id == target.System) {
                await ctx.RenderMemberReminderList(
                    _db,
                    target.Id,
                    $"Reminders for {target.Name}",
                    target.Color,
                    true);
            }
            else {
                await ctx.Reply($"{Emojis.Error} You can only view reminders from your own system.");
            }
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
