namespace StateSync.Server.Game;

public class Room
{
    public string RoomId { get; }
    public int MaxPlayers { get; }
    private readonly List<string> _players = [];
    private readonly object _lock = new();

    public Room(string roomId, int maxPlayers = 16)
    {
        RoomId = roomId;
        MaxPlayers = maxPlayers;
    }

    // 返回快照副本：调用方持有引用期间不阻塞写入，且不会观察到后续加入的玩家
    public IReadOnlyList<string> Players { get { lock (_lock) return [.. _players]; } }

    // lock 保证检查和加入是原子操作，防止两个线程同时通过满员检查
    public bool TryAdd(string playerId)
    {
        lock (_lock)
        {
            if (_players.Count >= MaxPlayers) return false;
            _players.Add(playerId);
            return true;
        }
    }
}
