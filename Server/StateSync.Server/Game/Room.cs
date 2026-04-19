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

    public IReadOnlyList<string> Players { get { lock (_lock) return [.. _players]; } }

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
