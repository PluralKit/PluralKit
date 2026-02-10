using Dapper;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Matrix;

public partial class MatrixRepository
{
    public async Task<AutoproxySettings> GetAutoproxySettings(SystemId systemId, string roomId)
    {
        await using var conn = await _db.Obtain();
        // Upsert: create default row if it doesn't exist, then return it
        return await conn.QueryFirstAsync<AutoproxySettings>(
            @"insert into matrix_autoproxy (system, room_id)
              values (@system, @roomId)
              on conflict (system, room_id) do update set system = @system
              returning *",
            new { system = systemId.Value, roomId });
    }

    public async Task UpdateAutoproxy(SystemId systemId, string roomId, AutoproxyPatch patch)
    {
        // Build SET clause dynamically from patch
        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("system", systemId.Value);
        parameters.Add("roomId", roomId);

        if (patch.AutoproxyMode.IsPresent)
        {
            setClauses.Add("autoproxy_mode = @mode");
            parameters.Add("mode", (int)patch.AutoproxyMode.Value);
        }

        if (patch.AutoproxyMember.IsPresent)
        {
            setClauses.Add("autoproxy_member = @member");
            parameters.Add("member", patch.AutoproxyMember.Value != null ? patch.AutoproxyMember.Value?.Value : null);
        }

        if (patch.LastLatchTimestamp.IsPresent)
        {
            setClauses.Add("last_latch_timestamp = @latch");
            parameters.Add("latch", patch.LastLatchTimestamp.Value.ToDateTimeOffset());
        }

        if (setClauses.Count == 0) return;

        var sql = $"update matrix_autoproxy set {string.Join(", ", setClauses)} where system = @system and room_id = @roomId";
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(sql, parameters);

        _logger.Information("Updated Matrix autoproxy for system {SystemId} in room {RoomId}", systemId, roomId);
    }
}
