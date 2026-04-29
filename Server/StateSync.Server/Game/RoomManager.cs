namespace StateSync.Server.Game;

using System.Collections.Concurrent;
using StateSync.Shared;

public class RoomManager
{
    private const int MaxAllowedPlayers = 16;
    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public void CreateRoom(string roomId, int maxPlayers = 16) =>
        _rooms[roomId] = new Room(roomId, maxPlayers);

    public (ErrorCode Error, int[] ErrorParams, JoinRoom? Response) HandleJoinRoom(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return (ErrorCode.RoomNotFound, [], null);

        var playerId = Guid.NewGuid().ToString("N")[..8];
        if (!room.TryAdd(playerId))
            return (ErrorCode.RoomFull, [room.MaxPlayers], null);

        var response = new JoinRoom { RoomId = roomId, PlayerId = playerId };
        response.PlayerIds.AddRange(room.Players);
        return (ErrorCode.Success, [], response);
    }

    public (ErrorCode Error, int[] ErrorParams, CreateRoom? Response) HandleCreateRoom(int maxPlayers)
    {
        if (maxPlayers <= 0 || maxPlayers > MaxAllowedPlayers)
            return (ErrorCode.InvalidMaxPlayers, [], null);

        var roomId = GenerateUniqueRoomId();
        _rooms[roomId] = new Room(roomId, maxPlayers);

        return (ErrorCode.Success, [], new CreateRoom { RoomId = roomId });
    }

    private string GenerateUniqueRoomId()
    {
        // Random.Shared 是线程安全的静态实例，无需 lock
        for (int i = 0; i < 10; i++)
        {
            var id = Random.Shared.Next(0, 1_000_000).ToString("D6");
            if (!_rooms.ContainsKey(id)) return id;
        }
        // 10 次碰撞后回退到 GUID 前缀，在百万房间规模内实际上不可能触发
        return Guid.NewGuid().ToString("N")[..6];
    }
}
