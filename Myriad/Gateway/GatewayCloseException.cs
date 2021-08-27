using System;

namespace Myriad.Gateway
{
    // TODO: unused?
    public class GatewayCloseException: Exception
    {
        public GatewayCloseException(int closeCode, string closeReason) : base($"{closeCode}: {closeReason}")
        {
            CloseCode = closeCode;
            CloseReason = closeReason;
        }

        public int CloseCode { get; }
        public string CloseReason { get; }
    }

    public class GatewayCloseCode
    {
        public const int UnknownError = 4000;
        public const int UnknownOpcode = 4001;
        public const int DecodeError = 4002;
        public const int NotAuthenticated = 4003;
        public const int AuthenticationFailed = 4004;
        public const int AlreadyAuthenticated = 4005;
        public const int InvalidSeq = 4007;
        public const int RateLimited = 4008;
        public const int SessionTimedOut = 4009;
        public const int InvalidShard = 4010;
        public const int ShardingRequired = 4011;
        public const int InvalidApiVersion = 4012;
        public const int InvalidIntent = 4013;
        public const int DisallowedIntent = 4014;
    }
}