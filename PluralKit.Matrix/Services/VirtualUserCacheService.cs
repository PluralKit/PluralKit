using System.Collections.Concurrent;

namespace PluralKit.Matrix;

public class VirtualUserCacheService
{
    private readonly ConcurrentDictionary<string, bool> _registeredUsers = new();
    private readonly ConcurrentDictionary<string, bool> _roomMemberships = new();

    private static string RoomKey(string mxid, string roomId) => $"{mxid}:{roomId}";

    public void MarkRegistered(string mxid) => _registeredUsers[mxid] = true;
    public bool IsRegistered(string mxid) => _registeredUsers.ContainsKey(mxid);

    public void MarkJoined(string mxid, string roomId) => _roomMemberships[RoomKey(mxid, roomId)] = true;
    public bool IsJoined(string mxid, string roomId) => _roomMemberships.ContainsKey(RoomKey(mxid, roomId));

    public void RemoveRoom(string mxid, string roomId) => _roomMemberships.TryRemove(RoomKey(mxid, roomId), out _);
}
