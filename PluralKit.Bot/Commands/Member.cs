using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Dapper;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Member
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly EmbedService _embeds;
        
        public Member(EmbedService embeds, IDatabase db, ModelRepository repo)
        {
            _embeds = embeds;
            _db = db;
            _repo = repo;
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
            
            // Send confirmation and space hint
            await ctx.Reply($"{Emojis.Success} Member \"{memberName}\" (`{member.Hid}`) registered! Check out the getting started page for how to get a member up and running: https://pluralkit.me/start#create-a-member");
            if (await _db.Execute(conn => conn.QuerySingleAsync<bool>("select has_private_members(@System)",
                new {System = ctx.System.Id}))) //if has private members
                await ctx.Reply($"{Emojis.Warn} This member is currently **public**. To change this, use `pk;member {member.Hid} private`.");

            if (memberName.Contains(" "))
                await ctx.Reply($"{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's 5-character ID (which is `{member.Hid}`).");
            if (memberCount >= memberLimit)
                await ctx.Reply($"{Emojis.Warn} You have reached the per-system member limit ({memberLimit}). You will be unable to create additional members until existing members are deleted.");
            else if (memberCount >= Limits.MaxMembersWarnThreshold(memberLimit))
                await ctx.Reply($"{Emojis.Warn} You are approaching the per-system member limit ({memberCount} / {memberLimit} members). Please review your member list for unused or duplicate members.");
        }
        
        public async Task MemberRandom(Context ctx)
        {
            ctx.CheckSystem();

            var randGen = new global::System.Random(); 
            //Maybe move this somewhere else in the file structure since it doesn't need to get created at every command

            // TODO: don't buffer these, find something else to do ig
            
            var members = await _db.Execute(c =>
            {
                if (ctx.MatchFlag("all", "a"))
                    return _repo.GetSystemMembers(c, ctx.System.Id);
                return _repo.GetSystemMembers(c, ctx.System.Id)
                    .Where(m => m.MemberVisibility == PrivacyLevel.Public);
            }).ToListAsync();
            
            if (members == null || !members.Any())
                throw Errors.NoMembersError;
            var randInt = randGen.Next(members.Count);
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(ctx.System, members[randInt], ctx.Guild, ctx.LookupContextFor(ctx.System)));
        }

        public async Task ViewMember(Context ctx, PKMember target)
        {
            var system = await _db.Execute(c => _repo.GetSystem(c, target.System));
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, target, ctx.Guild, ctx.LookupContextFor(system)));
        }
    }
}
