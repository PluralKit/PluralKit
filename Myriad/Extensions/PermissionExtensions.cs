using System;
using System.Collections.Generic;
using System.Linq;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Types;

namespace Myriad.Extensions
{
    public static class PermissionExtensions
    {
        public static PermissionSet PermissionsFor(this IDiscordCache cache, MessageCreateEvent message) =>
            PermissionsFor(cache, message.ChannelId, message.Author.Id, message.Member, isWebhook: message.WebhookId != null);

        public static PermissionSet PermissionsFor(this IDiscordCache cache, ulong channelId, GuildMember member) =>
            PermissionsFor(cache, channelId, member.User.Id, member);

        public static PermissionSet PermissionsFor(this IDiscordCache cache, ulong channelId, ulong userId, GuildMemberPartial? member, bool isWebhook = false)
        {
            if (!cache.TryGetChannel(channelId, out var channel))
                // todo: handle channel not found better
                return PermissionSet.Dm;

            if (channel.GuildId == null)
                return PermissionSet.Dm;

            var rootChannel = cache.GetRootChannel(channelId);

            var guild = cache.GetGuild(channel.GuildId.Value);

            if (isWebhook)
                return EveryonePermissions(guild);

            return PermissionsFor(guild, rootChannel, userId, member);
        }

        public static PermissionSet EveryonePermissions(this Guild guild) =>
            guild.Roles.FirstOrDefault(r => r.Id == guild.Id)?.Permissions ?? PermissionSet.Dm;

        public static PermissionSet EveryonePermissions(this IDiscordCache cache, Channel channel)
        {
            if (channel.Type == Channel.ChannelType.Dm)
                return PermissionSet.Dm;

            var defaultPermissions = cache.GetGuild(channel.GuildId!.Value).EveryonePermissions();
            var overwrite = channel.PermissionOverwrites?.FirstOrDefault(r => r.Id == channel.GuildId);
            if (overwrite == null)
                return defaultPermissions;

            var perms = defaultPermissions;
            perms &= ~overwrite.Deny;
            perms |= overwrite.Allow;

            return perms;
        }

        public static PermissionSet PermissionsFor(Guild guild, Channel channel, MessageCreateEvent msg) =>
            PermissionsFor(guild, channel, msg.Author.Id, msg.Member);

        public static PermissionSet PermissionsFor(Guild guild, Channel channel, ulong userId, GuildMemberPartial? member)
        {
            if (channel.Type == Channel.ChannelType.Dm)
                return PermissionSet.Dm;

            if (member == null)
                // this happens with system (Discord platform-owned) users - they're not actually in the guild, so there is no member object.
                return EveryonePermissions(guild);

            var perms = GuildPermissions(guild, userId, member.Roles);
            perms = ApplyChannelOverwrites(perms, channel, userId, member.Roles);

            if ((perms & PermissionSet.Administrator) == PermissionSet.Administrator)
                return PermissionSet.All;

            if ((perms & PermissionSet.ViewChannel) == 0)
                perms &= ~NeedsViewChannel;

            if ((perms & PermissionSet.SendMessages) == 0)
                perms &= ~NeedsSendMessages;

            return perms;
        }

        public static PermissionSet GuildPermissions(this Guild guild, ulong userId, ICollection<ulong> roleIds)
        {
            if (guild.OwnerId == userId)
                return PermissionSet.All;

            var perms = PermissionSet.None;
            foreach (var role in guild.Roles)
            {
                if (role.Id == guild.Id || roleIds.Contains(role.Id))
                    perms |= role.Permissions;
            }

            if (perms.HasFlag(PermissionSet.Administrator))
                return PermissionSet.All;

            return perms;
        }

        public static PermissionSet ApplyChannelOverwrites(PermissionSet perms, Channel channel, ulong userId,
                                                           ICollection<ulong> roleIds)
        {
            if (channel.PermissionOverwrites == null)
                return perms;

            var everyoneDeny = PermissionSet.None;
            var everyoneAllow = PermissionSet.None;
            var roleDeny = PermissionSet.None;
            var roleAllow = PermissionSet.None;
            var userDeny = PermissionSet.None;
            var userAllow = PermissionSet.None;

            foreach (var overwrite in channel.PermissionOverwrites)
            {
                switch (overwrite.Type)
                {
                    case Channel.OverwriteType.Role when overwrite.Id == channel.GuildId:
                        everyoneDeny |= overwrite.Deny;
                        everyoneAllow |= overwrite.Allow;
                        break;
                    case Channel.OverwriteType.Role when roleIds.Contains(overwrite.Id):
                        roleDeny |= overwrite.Deny;
                        roleAllow |= overwrite.Allow;
                        break;
                    case Channel.OverwriteType.Member when overwrite.Id == userId:
                        userDeny |= overwrite.Deny;
                        userAllow |= overwrite.Allow;
                        break;
                }
            }

            perms &= ~everyoneDeny;
            perms |= everyoneAllow;
            perms &= ~roleDeny;
            perms |= roleAllow;
            perms &= ~userDeny;
            perms |= userAllow;
            return perms;
        }

        private const PermissionSet NeedsViewChannel =
            PermissionSet.SendMessages |
            PermissionSet.SendTtsMessages |
            PermissionSet.ManageMessages |
            PermissionSet.EmbedLinks |
            PermissionSet.AttachFiles |
            PermissionSet.ReadMessageHistory |
            PermissionSet.MentionEveryone |
            PermissionSet.UseExternalEmojis |
            PermissionSet.AddReactions |
            PermissionSet.Connect |
            PermissionSet.Speak |
            PermissionSet.MuteMembers |
            PermissionSet.DeafenMembers |
            PermissionSet.MoveMembers |
            PermissionSet.UseVad |
            PermissionSet.Stream |
            PermissionSet.PrioritySpeaker;

        private const PermissionSet NeedsSendMessages =
            PermissionSet.MentionEveryone |
            PermissionSet.SendTtsMessages |
            PermissionSet.AttachFiles |
            PermissionSet.EmbedLinks;

        public static string ToPermissionString(this PermissionSet perms)
        {
            // TODO: clean string
            return perms.ToString();
        }
    }
}