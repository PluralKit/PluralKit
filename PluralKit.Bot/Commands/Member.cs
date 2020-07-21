using System.Linq;
using System.Threading.Tasks;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Member
    {
        private IDataStore _data;
        private IDatabase _db;
        private EmbedService _embeds;
        
        public Member(IDataStore data, EmbedService embeds, IDatabase db)
        {
            _data = data;
            _embeds = embeds;
            _db = db;
        }

        public async Task NewMember(Context ctx) {
            if (ctx.System == null) throw Errors.NoSystemError;
            var memberName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a member name.");
            
            // Hard name length cap
            if (memberName.Length > Limits.MaxMemberNameLength) throw Errors.MemberNameTooLongError(memberName.Length);

            // Warn if there's already a member by this name
            var existingMember = await _data.GetMemberByName(ctx.System, memberName);
            if (existingMember != null) {
                var msg = $"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.NameFor(ctx)}\" (with ID `{existingMember.Hid}`). Do you want to create another member with the same name?";
                if (!await ctx.PromptYesNo(msg)) throw new PKError("Member creation cancelled.");
            }

            await using var conn = await _db.Obtain();

            // Enforce per-system member limit
            var memberCount = await conn.GetSystemMemberCount(ctx.System.Id);
            if (memberCount >= Limits.MaxMemberCount)
                throw Errors.MemberLimitReachedError;

            // Create the member
            var member = await conn.CreateMember(ctx.System.Id, memberName);
            memberCount++;
            
            // Send confirmation and space hint
            await ctx.Reply($"{Emojis.Success} Member \"{memberName}\" (`{member.Hid}`) registered! Check out the getting started page for how to get a member up and running: https://pluralkit.me/start#members");
            if (memberName.Contains(" "))
                await ctx.Reply($"{Emojis.Note} Note that this member's name contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's 5-character ID (which is `{member.Hid}`).");
            if (memberCount >= Limits.MaxMemberCount)
                await ctx.Reply($"{Emojis.Warn} You have reached the per-system member limit ({Limits.MaxMemberCount}). You will be unable to create additional members until existing members are deleted.");
            else if (memberCount >= Limits.MaxMembersWarnThreshold)
                await ctx.Reply($"{Emojis.Warn} You are approaching the per-system member limit ({memberCount} / {Limits.MaxMemberCount} members). Please review your member list for unused or duplicate members.");
        }
        
        public async Task MemberRandom(Context ctx)
        {
            ctx.CheckSystem();

            var randGen = new global::System.Random(); 
            //Maybe move this somewhere else in the file structure since it doesn't need to get created at every command

            // TODO: don't buffer these, find something else to do ig
            var members = await _data.GetSystemMembers(ctx.System).Where(m => m.MemberVisibility == PrivacyLevel.Public).ToListAsync();
            if (members == null || !members.Any())
                throw Errors.NoMembersError;
            var randInt = randGen.Next(members.Count);
            var opts = ctx.ParseCardOptions(ctx.LookupContextFor(ctx.System));
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(ctx.System, members[randInt], ctx.Guild, ctx.LookupContextFor(ctx.System), opts));
        }

        public async Task ViewMember(Context ctx, PKMember target)
        {
            
            var system = await _db.Execute(c => c.QuerySystem(target.System));
            var opts = ctx.ParseCardOptions(ctx.LookupContextFor(system));
            await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, target, ctx.Guild, ctx.LookupContextFor(system), opts));
        }
    }
}