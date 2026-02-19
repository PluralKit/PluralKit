using Dapper;

using PluralKit.Core;

namespace PluralKit.Matrix;

public class MatrixVirtualUser
{
    public MemberId MemberId { get; set; }
    public string Mxid { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? AvatarMxc { get; set; }
    public DateTimeOffset? LastSynced { get; set; }
}

public partial class MatrixRepository
{
    public async Task<MatrixVirtualUser?> GetVirtualUser(MemberId memberId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<MatrixVirtualUser>(
            "select member_id, mxid, display_name, avatar_mxc, last_synced from matrix_virtual_users where member_id = @memberId",
            new { memberId = memberId.Value });
    }

    public async Task UpsertVirtualUser(MemberId memberId, string mxid, string? displayName, string? avatarMxc)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            @"insert into matrix_virtual_users (member_id, mxid, display_name, avatar_mxc, last_synced)
              values (@memberId, @mxid, @displayName, @avatarMxc, now())
              on conflict (member_id) do update set
                mxid = @mxid, display_name = @displayName,
                avatar_mxc = coalesce(@avatarMxc, matrix_virtual_users.avatar_mxc),
                last_synced = now()",
            new { memberId = memberId.Value, mxid, displayName, avatarMxc });
    }

    public async Task UpdateVirtualUserAvatar(MemberId memberId, string avatarMxc)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            "update matrix_virtual_users set avatar_mxc = @avatarMxc where member_id = @memberId",
            new { memberId = memberId.Value, avatarMxc });
    }

    public async Task UpdateVirtualUserSync(MemberId memberId, string displayName)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            "update matrix_virtual_users set display_name = @displayName, last_synced = now() where member_id = @memberId",
            new { memberId = memberId.Value, displayName });
    }

    public async Task<bool> CheckRoomJoined(string mxid, string roomId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<bool>(
            "select exists(select 1 from matrix_virtual_user_rooms where mxid = @mxid and room_id = @roomId)",
            new { mxid, roomId });
    }

    public async Task StoreRoomJoin(string mxid, string roomId)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            "insert into matrix_virtual_user_rooms (mxid, room_id) values (@mxid, @roomId) on conflict do nothing",
            new { mxid, roomId });
    }

    public async Task SetRoomBlacklisted(string roomId, bool blacklisted)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            @"insert into matrix_rooms (room_id, blacklisted) values (@roomId, @blacklisted)
              on conflict (room_id) do update set blacklisted = @blacklisted",
            new { roomId, blacklisted });
    }

    public async Task<string?> GetLogRoom(string roomId)
    {
        await using var conn = await _db.Obtain();
        return await conn.QueryFirstOrDefaultAsync<string?>(
            "select log_room from matrix_rooms where room_id = @roomId", new { roomId });
    }

    public async Task SetLogRoom(string roomId, string? logRoom)
    {
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync(
            @"insert into matrix_rooms (room_id, log_room) values (@roomId, @logRoom)
              on conflict (room_id) do update set log_room = @logRoom",
            new { roomId, logRoom });
    }
}
