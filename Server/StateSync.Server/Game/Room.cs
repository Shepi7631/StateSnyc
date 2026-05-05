namespace StateSync.Server.Game;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Pathfinding.Data;

public class Room
{
    public string RoomId { get; }
    public int MaxPlayers { get; }
    public MoveController MoveController { get; } = new();

    private readonly List<string> _players = [];
    private readonly Dictionary<string, PlayerEntity> _entities = new();
    private readonly List<IClientSession> _sessions = [];
    private readonly ConcurrentQueue<(string PlayerId, Vec2 Target)> _inputQueue = new();
    private readonly object _lock = new();
    private readonly NavMesh2D _navMesh;
    private readonly float _defaultSpeed;

    public Room(string roomId, int maxPlayers, NavMesh2D navMesh, float defaultSpeed = 5f)
    {
        RoomId = roomId;
        MaxPlayers = maxPlayers;
        _navMesh = navMesh;
        _defaultSpeed = defaultSpeed;
    }

    public NavMesh2D NavMesh => _navMesh;
    public uint CurrentTick { get; set; }

    public IReadOnlyList<string> Players { get { lock (_lock) return [.. _players]; } }

    public IReadOnlyList<IClientSession> Sessions { get { lock (_lock) return [.. _sessions]; } }

    public bool TryAdd(string playerId)
    {
        lock (_lock)
        {
            if (_players.Count >= MaxPlayers) return false;
            _players.Add(playerId);
            _entities[playerId] = new PlayerEntity(playerId, new Vec2(0f, 0f), _defaultSpeed, _navMesh);
            return true;
        }
    }

    public void AddSession(IClientSession session)
    {
        lock (_lock)
            _sessions.Add(session);
    }

    public void RemoveSession(IClientSession session)
    {
        lock (_lock)
            _sessions.Remove(session);
    }

    public void EnqueueMoveInput(string playerId, Vec2 target)
    {
        _inputQueue.Enqueue((playerId, target));
    }

    public PlayerEntity? GetEntity(string playerId)
    {
        lock (_lock)
            return _entities.GetValueOrDefault(playerId);
    }

    public IReadOnlyList<(string PlayerId, Vec2 Target)> DrainInputQueue()
    {
        var inputs = new List<(string, Vec2)>();
        while (_inputQueue.TryDequeue(out var input))
            inputs.Add(input);
        return inputs;
    }

    public void SyncRttFromSessions()
    {
        lock (_lock)
        {
            foreach (var session in _sessions)
            {
                if (session.PlayerId != null && _entities.TryGetValue(session.PlayerId, out var entity))
                    entity.Rtt = session.SmoothedRtt;
            }
        }
    }
}
