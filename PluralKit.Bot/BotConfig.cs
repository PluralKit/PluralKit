namespace PluralKit.Bot;

public class BotConfig
{
    public static readonly string[] DefaultPrefixes = { "pk;", "pk!" };

    public string Token { get; set; }
    public ulong ClientId { get; set; }

    // ASP.NET configuration merges arrays with defaults, so we leave this field nullable
    // and fall back to the separate default array at the use site :)
    // This does bind [] as null (therefore default) instead of an empty array, but I can live w/ that.
    public string[] Prefixes { get; set; }

    public int? MaxShardConcurrency { get; set; }

    public ulong? AdminRole { get; set; }

    public ClusterSettings? Cluster { get; set; }

    public string? GatewayQueueUrl { get; set; }
    public bool UseRedisRatelimiter { get; set; } = false;

    public string? HttpCacheUrl { get; set; }
    public bool HttpUseInnerCache { get; set; } = false;

    public string? HttpListenerAddr { get; set; }
    public bool DisableGateway { get; set; } = false;
    public string? EventAwaiterTarget { get; set; }

    public string? DiscordBaseUrl { get; set; }
    public string? AvatarServiceUrl { get; set; }

    public bool DisableErrorReporting { get; set; } = false;

    public bool IsBetaBot { get; set; } = false!;
    public string BetaBotAPIUrl { get; set; }

    public record ClusterSettings
    {
        // this is zero-indexed
        public string NodeName { get; set; }
        public int TotalShards { get; set; }
        public int TotalNodes { get; set; }

        // Node name eg. "pluralkit-3", want to extract the 3. blame k8s :p
        public int NodeIndex => int.Parse(NodeName.Split("-").Last());
    }
}