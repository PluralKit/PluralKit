using System.Collections.Generic;

namespace Myriad.Gateway
{
    public record ClusterSessionState
    {
        public List<ShardState> Shards { get; init; }

        public record ShardState
        {
            public ShardInfo Shard { get; init; }
            public ShardSessionInfo Session { get; init; }
        }
    }
}