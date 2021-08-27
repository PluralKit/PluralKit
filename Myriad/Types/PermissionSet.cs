using System;

namespace Myriad.Types
{
    [Flags]
    public enum PermissionSet: ulong
    {
        CreateInvite = 0x1,
        KickMembers = 0x2,
        BanMembers = 0x4,
        Administrator = 0x8,
        ManageChannels = 0x10,
        ManageGuild = 0x20,
        AddReactions = 0x40,
        ViewAuditLog = 0x80,
        PrioritySpeaker = 0x100,
        Stream = 0x200,
        ViewChannel = 0x400,
        SendMessages = 0x800,
        SendTtsMessages = 0x1000,
        ManageMessages = 0x2000,
        EmbedLinks = 0x4000,
        AttachFiles = 0x8000,
        ReadMessageHistory = 0x10000,
        MentionEveryone = 0x20000,
        UseExternalEmojis = 0x40000,
        ViewGuildInsights = 0x80000,
        Connect = 0x100000,
        Speak = 0x200000,
        MuteMembers = 0x400000,
        DeafenMembers = 0x800000,
        MoveMembers = 0x1000000,
        UseVad = 0x2000000,
        ChangeNickname = 0x4000000,
        ManageNicknames = 0x8000000,
        ManageRoles = 0x10000000,
        ManageWebhooks = 0x20000000,
        ManageEmojis = 0x40000000,

        // Special:
        None = 0,
        All = 0x7FFFFFFF,

        Dm = ViewChannel | SendMessages | ReadMessageHistory | AddReactions | AttachFiles | EmbedLinks |
             UseExternalEmojis | Connect | Speak | UseVad
    }
}