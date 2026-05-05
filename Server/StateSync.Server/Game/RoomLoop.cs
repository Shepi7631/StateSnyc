namespace StateSync.Server.Game;

using Google.Protobuf;
using StateSync.Shared;

public class RoomLoop
{
    private readonly Room _room;
    private readonly GameLoop _gameLoop;
    private uint _pingSequence;

    public RoomLoop(Room room, int tickRate = 10)
    {
        _room = room;
        _gameLoop = new GameLoop(tickRate);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_gameLoop.DeltaTime);
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            Tick();
        }
    }

    public void Tick()
    {
        _gameLoop.Tick();
        _room.CurrentTick = _gameLoop.CurrentTick;
        ProcessInputs();
        _room.MoveController.Tick(_gameLoop.DeltaTime);
        BroadcastPositionSync();
        BroadcastPing();
        _room.SyncRttFromSessions();
        _gameLoop.Flush();
    }

    private void ProcessInputs()
    {
        var inputs = _room.DrainInputQueue();
        var sessions = _room.Sessions;
        foreach (var (playerId, target) in inputs)
        {
            var entity = _room.GetEntity(playerId);
            if (entity == null) continue;

            if (_room.MoveController.StartMove(entity, target))
            {
                var moveEvent = new MoveEvent
                {
                    PlayerId = playerId,
                    TargetX = target.X,
                    TargetY = target.Z,
                    Speed = entity.Speed,
                    Tick = _gameLoop.CurrentTick
                };
                _gameLoop.EnqueueBroadcast(sessions, MessageType.MoveEvent, moveEvent.ToByteArray());
            }
        }
    }

    private void BroadcastPositionSync()
    {
        if (!_room.MoveController.HasMovingEntities)
            return;

        var sessions = _room.Sessions;
        foreach (var entity in _room.MoveController.GetMovingEntities())
        {
            var sync = new PositionSync
            {
                PlayerId = entity.PlayerId,
                PosX = entity.Position.X,
                PosY = entity.Position.Z,
                IsMoving = entity.IsMoving,
                Tick = _gameLoop.CurrentTick
            };
            _gameLoop.EnqueueBroadcast(sessions, MessageType.PositionSync, sync.ToByteArray());
        }
    }

    private void BroadcastPing()
    {
        if (_gameLoop.CurrentTick % 10 != 0)
            return;

        _pingSequence++;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sessions = _room.Sessions;

        foreach (var session in sessions)
        {
            var ping = new Ping
            {
                ServerTimestamp = (ulong)now,
                Sequence = _pingSequence,
                YourRttMs = (uint)session.SmoothedRtt
            };
            _gameLoop.EnqueueSend(session, MessageType.Ping, ping.ToByteArray());
        }
    }
}
