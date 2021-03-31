using System.Collections.Generic;

using Myriad.Types;

namespace Myriad.Gateway
{
    public record GatewayStatusUpdate
    {
        public enum UserStatus
        {
            Online,
            Dnd,
            Idle,
            Invisible,
            Offline
        }

        public ulong? Since { get; init; }
        public ActivityPartial[]? Activities { get; init; }
        public UserStatus Status { get; init; }
        public bool Afk { get; init; }
    }
}