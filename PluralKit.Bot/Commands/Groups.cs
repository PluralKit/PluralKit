using System.Threading.Tasks;

using DSharpPlus.Entities;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Groups
    {
        private readonly IDatabase _db;

        public Groups(IDatabase db)
        {
            _db = db;
        }

        public async Task CreateGroup(Context ctx)
        {
            ctx.CheckSystem();
            
            var groupName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a group name.");
            if (groupName.Length > Limits.MaxGroupNameLength)
                throw new PKError($"Group name too long ({groupName.Length}/{Limits.MaxMemberNameLength} characters).");
            
            await using var conn = await _db.Obtain();
            var newGroup = await conn.CreateGroup(ctx.System.Id, groupName);

            await ctx.Reply($"{Emojis.Success} Group \"**{groupName}**\" (`{newGroup.Hid}`) registered!\nYou can now start adding members to the group:\n- **pk;group {newGroup.Hid} add <members...>**");
        }

        public async Task ShowGroupCard(Context ctx, PKGroup target)
        {
            await using var conn = await _db.Obtain();
            
            var system = await GetGroupSystem(ctx, target, conn);

            var nameField = target.Name;
            if (system.Name != null)
                nameField = $"{nameField} ({system.Name})";

            var eb = new DiscordEmbedBuilder()
                .WithAuthor(nameField)
                .WithDescription(target.Description)
                .WithFooter($"System ID: {system.Hid} | Group ID: {target.Hid} | Created on {target.Created.FormatZoned(system)}");

            await ctx.Reply(embed: eb.Build());
        }

        private static async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target, IPKConnection conn)
        {
            var system = ctx.System;
            if (system?.Id == target.System)
                return system;
            return await conn.QuerySystem(target.System)!;
        }
    }
}