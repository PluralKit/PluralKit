using Dapper;

using NodaTime;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<IEnumerable<PKShardInfo>> GetShards() =>
        _db.Execute(conn => conn.QueryAsync<PKShardInfo>("select * from shards order by id"));

    public Task SetShardStatus(IPKConnection conn, int shard, PKShardInfo.ShardStatus status) =>
        conn.ExecuteAsync(
            "insert into shards (id, status) values (@Id, @Status) on conflict (id) do update set status = @Status",
            new { Id = shard, Status = status });

    public Task ClearShardStatus() => _db.Execute(conn => conn.ExecuteAsync("update shards set status = 0"));

    public Task RegisterShardHeartbeat(IPKConnection conn, int shard, Duration ping) =>
        conn.ExecuteAsync(
            "insert into shards (id, last_heartbeat, ping) values (@Id, now(), @Ping) on conflict (id) do update set last_heartbeat = now(), ping = @Ping",
            new { Id = shard, Ping = ping.TotalSeconds });

    public Task RegisterShardConnection(IPKConnection conn, int shard) =>
        conn.ExecuteAsync(
            "insert into shards (id, last_connection) values (@Id, now()) on conflict (id) do update set last_connection = now()",
            new { Id = shard });
}