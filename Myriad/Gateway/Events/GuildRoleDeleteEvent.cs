namespace Myriad.Gateway
{
    public record GuildRoleDeleteEvent(ulong GuildId, ulong RoleId): IGatewayEvent;
}