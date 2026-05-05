# Movement Synchronization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement click-to-move synchronization with authoritative server, client-side prediction, and drift correction.

**Architecture:** Client sends MoveRequest on click and immediately begins local pathfinding+movement. Server validates, pathfinds, simulates movement at 10 Hz, broadcasts MoveEvent (once per move start) and PositionSync (periodic correction). All clients run local prediction for all players; PositionSync corrects drift.

**Tech Stack:** .NET 9 server, Unity client (netstandard2.1 shared lib), Protobuf, shared Pathfinding.Core (A* + Funnel on NavMesh2D)

---

## Task 1: Add Vec2.Normalized to Shared Pathfinding.Core

The movement tick logic requires normalizing direction vectors. Vec2 currently lacks this.

**Files:**
- Modify: `Share/Pathfinding.Core/Data/Vec2.cs`
- Test: `Share/Pathfinding.Core.Tests/Data/Vec2Tests.cs`

- [ ] **Step 1: Write failing tests**

Add to `Share/Pathfinding.Core.Tests/Data/Vec2Tests.cs`:

```csharp
[Fact]
public void Normalized_UnitVector_ReturnsSameDirection()
{
    var v = new Vec2(3f, 4f);
    var n = v.Normalized();
    Assert.Equal(0.6f, n.X, precision: 5);
    Assert.Equal(0.8f, n.Z, precision: 5);
}

[Fact]
public void Normalized_ZeroVector_ReturnsZero()
{
    var v = new Vec2(0f, 0f);
    var n = v.Normalized();
    Assert.Equal(0f, n.X);
    Assert.Equal(0f, n.Z);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Share/Pathfinding.Core.Tests --filter "Normalized" -v n`
Expected: Compilation error — `Vec2` has no `Normalized` method.

- [ ] **Step 3: Implement Normalized**

In `Share/Pathfinding.Core/Data/Vec2.cs`, add after `DistanceTo`:

```csharp
public Vec2 Normalized()
{
    float len = MathF.Sqrt(X * X + Z * Z);
    if (len == 0f) return new Vec2(0f, 0f);
    return new Vec2(X / len, Z / len);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Share/Pathfinding.Core.Tests --filter "Normalized" -v n`
Expected: 2 tests PASS.

- [ ] **Step 5: Commit**

```
git add Share/Pathfinding.Core/Data/Vec2.cs Share/Pathfinding.Core.Tests/Data/Vec2Tests.cs
git commit -m "feat: add Vec2.Normalized for movement direction calculation"
```

---

## Task 2: Update Proto Definitions

Add MoveRequest, MoveEvent, PositionSync messages and new MessageType entries.

**Files:**
- Modify: `Share/Proto/messages.proto`
- Modify: `Share/Proto/message_type.proto`

- [ ] **Step 1: Update messages.proto**

Replace the full content of `Share/Proto/messages.proto` with:

```protobuf
syntax = "proto3";
option csharp_namespace = "StateSync.Shared";

message JoinRoom {
    string room_id = 1;
    string player_id = 2;
    repeated string player_ids = 3;
}

message PlayerJoined {
    string player_id = 1;
    string room_id = 2;
}

message CreateRoom {
    int32 max_players = 1;
    string room_id = 2;
}

message MoveRequest {
    float target_x = 1;
    float target_y = 2;
}

message MoveEvent {
    string player_id = 1;
    float target_x = 2;
    float target_y = 3;
    float speed = 4;
    uint32 tick = 5;
}

message PositionSync {
    string player_id = 1;
    float pos_x = 2;
    float pos_y = 3;
    bool is_moving = 4;
    uint32 tick = 5;
}
```

- [ ] **Step 2: Update message_type.proto**

Replace the full content of `Share/Proto/message_type.proto` with:

```protobuf
syntax = "proto3";
option csharp_namespace = "StateSync.Shared";

enum MessageType {
    UNSPECIFIED = 0;
    JOIN_ROOM = 1;
    LEAVE_ROOM = 2;
    PLAYER_JOINED = 3;
    PLAYER_INPUT = 4;
    WORLD_STATE = 5;
    CREATE_ROOM = 6;
    MOVE_REQUEST = 7;
    MOVE_EVENT = 8;
    POSITION_SYNC = 9;
}
```

- [ ] **Step 3: Verify server project builds with new protos**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds. New generated classes `MoveRequest`, `MoveEvent`, `PositionSync` available in `StateSync.Shared` namespace.

- [ ] **Step 4: Commit**

```
git add Share/Proto/messages.proto Share/Proto/message_type.proto
git commit -m "feat: add MoveRequest, MoveEvent, PositionSync proto messages"
```

---

## Task 3: Implement PlayerEntity (Server)

Server-side per-player movement state with pathfinding and tick logic.

**Files:**
- Create: `Server/StateSync.Server/Game/PlayerEntity.cs`
- Test: `Server/StateSync.Server.Tests/Game/PlayerEntityTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Server/StateSync.Server.Tests/Game/PlayerEntityTests.cs`:

```csharp
namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using Pathfinding.Data;
using Pathfinding.Algorithm;

public class PlayerEntityTests
{
    private static NavMesh2D BuildSimpleMesh()
    {
        // Simple square mesh: two triangles forming a 10x10 square
        var vertices = new Vec2[]
        {
            new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f)
        };
        var triangles = new NavTriangle[]
        {
            new(0, 1, 2, 1, -1, -1),
            new(0, 2, 3, -1, -1, 0)
        };
        return new NavMesh2D(vertices, triangles);
    }

    [Fact]
    public void SetDestination_ValidTarget_ReturnsTrue()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);

        bool result = entity.SetDestination(new Vec2(9f, 9f));

        Assert.True(result);
        Assert.True(entity.IsMoving);
    }

    [Fact]
    public void SetDestination_TargetOutsideMesh_ReturnsFalse()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);

        bool result = entity.SetDestination(new Vec2(100f, 100f));

        Assert.False(result);
        Assert.False(entity.IsMoving);
    }

    [Fact]
    public void Tick_MovesAlongPath()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
        entity.SetDestination(new Vec2(9f, 1f));

        entity.Tick(0.1f); // 5 * 0.1 = 0.5 units

        float distance = entity.Position.DistanceTo(new Vec2(1f, 1f));
        Assert.True(distance > 0.4f);
        Assert.True(entity.IsMoving);
    }

    [Fact]
    public void Tick_ReachesDestination_StopsMoving()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 100f, mesh);
        entity.SetDestination(new Vec2(2f, 1f));

        entity.Tick(1f); // 100 * 1 = 100 units, far exceeds path length

        Assert.False(entity.IsMoving);
    }

    [Fact]
    public void Tick_WhenNotMoving_DoesNothing()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
        var posBefore = entity.Position;

        entity.Tick(0.1f);

        Assert.Equal(posBefore, entity.Position);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Server/StateSync.Server.Tests --filter "PlayerEntity" -v n`
Expected: Compilation error — `PlayerEntity` class does not exist.

- [ ] **Step 3: Implement PlayerEntity**

Create `Server/StateSync.Server/Game/PlayerEntity.cs`:

```csharp
namespace StateSync.Server.Game;

using Pathfinding.Data;
using Pathfinding.Algorithm;
using System.Collections.Generic;

public class PlayerEntity
{
    public string PlayerId { get; }
    public Vec2 Position { get; private set; }
    public float Speed { get; }
    public bool IsMoving { get; private set; }
    public Vec2 Target { get; private set; }

    private readonly NavMesh2D _mesh;
    private Vec2[] _path = [];
    private int _pathIndex;

    public PlayerEntity(string playerId, Vec2 position, float speed, NavMesh2D mesh)
    {
        PlayerId = playerId;
        Position = position;
        Speed = speed;
        _mesh = mesh;
    }

    public bool SetDestination(Vec2 target)
    {
        var corridor = AStarSolver.Solve(_mesh, Position, target);
        if (corridor == null)
            return false;

        var waypoints = FunnelSolver.Solve(_mesh, corridor, Position, target);
        _path = ToArray(waypoints);
        _pathIndex = 0;
        Target = target;
        IsMoving = true;
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!IsMoving)
            return;

        float remaining = Speed * deltaTime;
        while (remaining > 0f && _pathIndex < _path.Length - 1)
        {
            Vec2 next = _path[_pathIndex + 1];
            float dist = Position.DistanceTo(next);

            if (remaining >= dist)
            {
                Position = next;
                _pathIndex++;
                remaining -= dist;
            }
            else
            {
                Vec2 direction = (next - Position).Normalized();
                Position = Position + direction * remaining;
                remaining = 0f;
            }
        }

        if (_pathIndex >= _path.Length - 1)
            IsMoving = false;
    }

    private static Vec2[] ToArray(IReadOnlyList<Vec2> list)
    {
        var arr = new Vec2[list.Count];
        for (int i = 0; i < list.Count; i++)
            arr[i] = list[i];
        return arr;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Server/StateSync.Server.Tests --filter "PlayerEntity" -v n`
Expected: 5 tests PASS.

- [ ] **Step 5: Commit**

```
git add Server/StateSync.Server/Game/PlayerEntity.cs Server/StateSync.Server.Tests/Game/PlayerEntityTests.cs
git commit -m "feat: implement PlayerEntity with pathfinding and tick movement"
```

---

## Task 4: Implement MoveController (Server)

Manages the set of actively moving players. Builds MoveEvent and PositionSync messages.

**Files:**
- Create: `Server/StateSync.Server/Game/MoveController.cs`
- Test: `Server/StateSync.Server.Tests/Game/MoveControllerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Server/StateSync.Server.Tests/Game/MoveControllerTests.cs`:

```csharp
namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using Pathfinding.Data;
using Pathfinding.Algorithm;

public class MoveControllerTests
{
    private static NavMesh2D BuildSimpleMesh()
    {
        var vertices = new Vec2[]
        {
            new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f)
        };
        var triangles = new NavTriangle[]
        {
            new(0, 1, 2, 1, -1, -1),
            new(0, 2, 3, -1, -1, 0)
        };
        return new NavMesh2D(vertices, triangles);
    }

    [Fact]
    public void StartMove_ValidTarget_AddsToMoving()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
        var controller = new MoveController();

        bool result = controller.StartMove(entity, new Vec2(9f, 9f));

        Assert.True(result);
        Assert.True(controller.HasMovingEntities);
    }

    [Fact]
    public void StartMove_InvalidTarget_ReturnsFalse()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
        var controller = new MoveController();

        bool result = controller.StartMove(entity, new Vec2(100f, 100f));

        Assert.False(result);
        Assert.False(controller.HasMovingEntities);
    }

    [Fact]
    public void Tick_EntityReachesDestination_RemovesFromMoving()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 100f, mesh);
        var controller = new MoveController();
        controller.StartMove(entity, new Vec2(2f, 1f));

        controller.Tick(1f);

        Assert.False(controller.HasMovingEntities);
    }

    [Fact]
    public void Tick_EntityStillMoving_RemainsInSet()
    {
        var mesh = BuildSimpleMesh();
        var entity = new PlayerEntity("p1", new Vec2(1f, 1f), 1f, mesh);
        var controller = new MoveController();
        controller.StartMove(entity, new Vec2(9f, 1f));

        controller.Tick(0.1f);

        Assert.True(controller.HasMovingEntities);
    }

    [Fact]
    public void GetMovingEntities_ReturnsCurrentlyMoving()
    {
        var mesh = BuildSimpleMesh();
        var e1 = new PlayerEntity("p1", new Vec2(1f, 1f), 5f, mesh);
        var e2 = new PlayerEntity("p2", new Vec2(2f, 2f), 5f, mesh);
        var controller = new MoveController();
        controller.StartMove(e1, new Vec2(9f, 1f));
        controller.StartMove(e2, new Vec2(9f, 2f));

        var moving = controller.GetMovingEntities();

        Assert.Equal(2, moving.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Server/StateSync.Server.Tests --filter "MoveController" -v n`
Expected: Compilation error — `MoveController` class does not exist.

- [ ] **Step 3: Implement MoveController**

Create `Server/StateSync.Server/Game/MoveController.cs`:

```csharp
namespace StateSync.Server.Game;

using System.Collections.Generic;
using Pathfinding.Data;

public class MoveController
{
    private readonly HashSet<PlayerEntity> _movingEntities = [];
    private readonly List<PlayerEntity> _toRemove = [];

    public bool HasMovingEntities => _movingEntities.Count > 0;

    public bool StartMove(PlayerEntity entity, Vec2 target)
    {
        if (!entity.SetDestination(target))
            return false;

        _movingEntities.Add(entity);
        return true;
    }

    public void StopMove(PlayerEntity entity)
    {
        _movingEntities.Remove(entity);
    }

    public void Tick(float deltaTime)
    {
        _toRemove.Clear();
        foreach (var entity in _movingEntities)
        {
            entity.Tick(deltaTime);
            if (!entity.IsMoving)
                _toRemove.Add(entity);
        }
        foreach (var entity in _toRemove)
            _movingEntities.Remove(entity);
    }

    public IReadOnlyCollection<PlayerEntity> GetMovingEntities() => _movingEntities;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Server/StateSync.Server.Tests --filter "MoveController" -v n`
Expected: 5 tests PASS.

- [ ] **Step 5: Commit**

```
git add Server/StateSync.Server/Game/MoveController.cs Server/StateSync.Server.Tests/Game/MoveControllerTests.cs
git commit -m "feat: implement MoveController to manage actively moving entities"
```

---

## Task 5: Implement GameLoop (Server)

Fixed-rate loop with module-driven packet registration and flush.

**Files:**
- Create: `Server/StateSync.Server/Game/GameLoop.cs`
- Test: `Server/StateSync.Server.Tests/Game/GameLoopTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Server/StateSync.Server.Tests/Game/GameLoopTests.cs`:

```csharp
namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using StateSync.Shared;

public class GameLoopTests
{
    [Fact]
    public void Tick_IncrementsTick()
    {
        var loop = new GameLoop(tickRate: 10);

        Assert.Equal(0u, loop.CurrentTick);
        loop.Tick();
        Assert.Equal(1u, loop.CurrentTick);
    }

    [Fact]
    public void DeltaTime_Is_InverseOfTickRate()
    {
        var loop = new GameLoop(tickRate: 10);

        Assert.Equal(0.1f, loop.DeltaTime, precision: 5);
    }

    [Fact]
    public void EnqueueBroadcast_And_Flush_ClearsQueue()
    {
        var loop = new GameLoop(tickRate: 10);
        var flushed = new List<(MessageType Type, byte[] Data)>();
        var session = new FakeSession(flushed);

        loop.EnqueueBroadcast([session], MessageType.MoveEvent, [1, 2, 3]);
        loop.Flush();

        Assert.Single(flushed);
        Assert.Equal(MessageType.MoveEvent, flushed[0].Type);
        Assert.Equal(new byte[] { 1, 2, 3 }, flushed[0].Data);
    }

    [Fact]
    public void Flush_EmptyQueue_DoesNothing()
    {
        var loop = new GameLoop(tickRate: 10);
        loop.Flush(); // no exception
    }

    [Fact]
    public void EnqueueSend_SingleSession()
    {
        var loop = new GameLoop(tickRate: 10);
        var flushed = new List<(MessageType Type, byte[] Data)>();
        var session = new FakeSession(flushed);

        loop.EnqueueSend(session, MessageType.PositionSync, [4, 5]);
        loop.Flush();

        Assert.Single(flushed);
        Assert.Equal(MessageType.PositionSync, flushed[0].Type);
    }

    private class FakeSession : IClientSession
    {
        private readonly List<(MessageType, byte[])> _log;
        public FakeSession(List<(MessageType, byte[])> log) => _log = log;
        public void Send(MessageType type, byte[] data) => _log.Add((type, data));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Server/StateSync.Server.Tests --filter "GameLoop" -v n`
Expected: Compilation error — `GameLoop` and `IClientSession` do not exist.

- [ ] **Step 3: Implement IClientSession interface**

Create `Server/StateSync.Server/Network/IClientSession.cs`:

```csharp
namespace StateSync.Server.Game;

using StateSync.Shared;

public interface IClientSession
{
    void Send(MessageType type, byte[] data);
}
```

- [ ] **Step 4: Implement GameLoop**

Create `Server/StateSync.Server/Game/GameLoop.cs`:

```csharp
namespace StateSync.Server.Game;

using System.Collections.Generic;
using StateSync.Shared;

public class GameLoop
{
    private readonly List<(IReadOnlyList<IClientSession> Sessions, MessageType Type, byte[] Data)> _outgoing = [];

    public uint CurrentTick { get; private set; }
    public float DeltaTime { get; }

    public GameLoop(int tickRate)
    {
        DeltaTime = 1f / tickRate;
    }

    public void Tick()
    {
        CurrentTick++;
    }

    public void EnqueueBroadcast(IReadOnlyList<IClientSession> sessions, MessageType type, byte[] data)
    {
        _outgoing.Add((sessions, type, data));
    }

    public void EnqueueSend(IClientSession session, MessageType type, byte[] data)
    {
        _outgoing.Add(([session], type, data));
    }

    public void Flush()
    {
        foreach (var (sessions, type, data) in _outgoing)
        {
            foreach (var session in sessions)
                session.Send(type, data);
        }
        _outgoing.Clear();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Server/StateSync.Server.Tests --filter "GameLoop" -v n`
Expected: 5 tests PASS.

- [ ] **Step 6: Commit**

```
git add Server/StateSync.Server/Game/GameLoop.cs Server/StateSync.Server/Network/IClientSession.cs Server/StateSync.Server.Tests/Game/GameLoopTests.cs
git commit -m "feat: implement GameLoop with module-driven packet queue and flush"
```

---

## Task 6: Implement ClientSession (Server Network Layer)

Wrap a TCP connection with player/room binding and push capability implementing IClientSession.

**Files:**
- Create: `Server/StateSync.Server/Network/ClientSession.cs`

- [ ] **Step 1: Implement ClientSession**

Create `Server/StateSync.Server/Network/ClientSession.cs`:

```csharp
namespace StateSync.Server.Network;

using System.Buffers;
using System.Net.Sockets;
using StateSync.Server.Game;
using StateSync.Shared;

public class ClientSession : IClientSession
{
    private readonly NetworkStream _stream;
    private readonly object _writeLock = new();

    public string? PlayerId { get; set; }
    public Room? Room { get; set; }

    public ClientSession(NetworkStream stream)
    {
        _stream = stream;
    }

    public void Send(MessageType type, byte[] data)
    {
        var (buffer, length) = PacketWriter.WriteServerPacket(type, ErrorCode.Success, [], data);
        try
        {
            lock (_writeLock)
            {
                _stream.Write(buffer, 0, length);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```
git add Server/StateSync.Server/Network/ClientSession.cs
git commit -m "feat: implement ClientSession with IClientSession push support"
```

---

## Task 7: Refactor Room to Hold Entities and Sessions

Room needs to manage PlayerEntity instances, a MoveController, and a list of connected sessions.

**Files:**
- Modify: `Server/StateSync.Server/Game/Room.cs`
- Modify: `Server/StateSync.Server.Tests/Game/RoomManagerTests.cs` (ensure existing tests still pass)

- [ ] **Step 1: Refactor Room**

Replace `Server/StateSync.Server/Game/Room.cs` with:

```csharp
namespace StateSync.Server.Game;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Pathfinding.Data;
using StateSync.Server.Network;

public class Room
{
    public string RoomId { get; }
    public int MaxPlayers { get; }
    public MoveController MoveController { get; } = new();

    private readonly List<string> _players = [];
    private readonly Dictionary<string, PlayerEntity> _entities = new();
    private readonly List<ClientSession> _sessions = [];
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

    public IReadOnlyList<string> Players { get { lock (_lock) return [.. _players]; } }

    public IReadOnlyList<ClientSession> Sessions { get { lock (_lock) return [.. _sessions]; } }

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

    public void AddSession(ClientSession session)
    {
        lock (_lock)
            _sessions.Add(session);
    }

    public void RemoveSession(ClientSession session)
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
}
```

- [ ] **Step 2: Update RoomManager to pass NavMesh**

Modify `Server/StateSync.Server/Game/RoomManager.cs` — the `CreateRoom` method now requires a NavMesh2D. Update the signature:

```csharp
namespace StateSync.Server.Game;

using System.Collections.Concurrent;
using Pathfinding.Data;
using StateSync.Shared;

public class RoomManager
{
    private const int MaxAllowedPlayers = 16;
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly NavMesh2D _navMesh;

    public RoomManager(NavMesh2D navMesh)
    {
        _navMesh = navMesh;
    }

    public void CreateRoom(string roomId, int maxPlayers = 16) =>
        _rooms[roomId] = new Room(roomId, maxPlayers, _navMesh);

    public Room? GetRoom(string roomId) =>
        _rooms.GetValueOrDefault(roomId);

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
        _rooms[roomId] = new Room(roomId, maxPlayers, _navMesh);

        return (ErrorCode.Success, [], new CreateRoom { RoomId = roomId });
    }

    private string GenerateUniqueRoomId()
    {
        for (int i = 0; i < 10; i++)
        {
            var id = Random.Shared.Next(0, 1_000_000).ToString("D6");
            if (!_rooms.ContainsKey(id)) return id;
        }
        return Guid.NewGuid().ToString("N")[..6];
    }
}
```

- [ ] **Step 3: Fix RoomManagerTests**

Update `Server/StateSync.Server.Tests/Game/RoomManagerTests.cs` to supply a NavMesh:

```csharp
namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using StateSync.Shared;
using Pathfinding.Data;

public class RoomManagerTests
{
    private readonly RoomManager _manager;

    public RoomManagerTests()
    {
        var vertices = new Vec2[]
        {
            new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f)
        };
        var triangles = new NavTriangle[]
        {
            new(0, 1, 2, 1, -1, -1),
            new(0, 2, 3, -1, -1, 0)
        };
        var mesh = new NavMesh2D(vertices, triangles);
        _manager = new RoomManager(mesh);
        _manager.CreateRoom("room1", maxPlayers: 2);
    }

    // ... all existing test methods remain unchanged ...
}
```

- [ ] **Step 4: Run all existing tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All existing tests PASS.

- [ ] **Step 5: Commit**

```
git add Server/StateSync.Server/Game/Room.cs Server/StateSync.Server/Game/RoomManager.cs Server/StateSync.Server.Tests/Game/RoomManagerTests.cs
git commit -m "refactor: Room holds PlayerEntities, sessions, and input queue"
```

---

## Task 8: Refactor TcpServer for ClientSession and Bidirectional Communication

Replace the request-response model with ClientSession-based connection handling that supports push.

**Files:**
- Modify: `Server/StateSync.Server/Network/TcpServer.cs`
- Modify: `Server/StateSync.Server/Network/MessageDispatcher.cs`

- [ ] **Step 1: Refactor MessageDispatcher**

The dispatcher now receives a ClientSession context and handles MoveRequest by queueing into the room. Replace `Server/StateSync.Server/Network/MessageDispatcher.cs`:

```csharp
namespace StateSync.Server.Network;

using System.Buffers;
using Google.Protobuf;
using StateSync.Server.Game;
using StateSync.Shared;
using Pathfinding.Data;

public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    public MessageDispatcher(RoomManager roomManager) => _roomManager = roomManager;

    public (byte[]? Buffer, int Length) Dispatch(MessageType type, byte[] data, int dataLength, ClientSession session) => type switch
    {
        MessageType.JoinRoom => HandleJoinRoom(data, dataLength, session),
        MessageType.CreateRoom => HandleCreateRoom(data, dataLength),
        MessageType.MoveRequest => HandleMoveRequest(data, dataLength, session),
        _ => WriteResponse(type, ErrorCode.InvalidInput, [], [])
    };

    private (byte[] Buffer, int Length) HandleJoinRoom(byte[] data, int dataLength, ClientSession session)
    {
        JoinRoom request;
        try { request = JoinRoom.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return WriteResponse(MessageType.JoinRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleJoinRoom(request.RoomId);
        if (error == ErrorCode.Success && response != null)
        {
            session.PlayerId = response.PlayerId;
            var room = _roomManager.GetRoom(request.RoomId);
            if (room != null)
            {
                session.Room = room;
                room.AddSession(session);
            }
        }
        byte[] responseData = response?.ToByteArray() ?? [];
        return WriteResponse(MessageType.JoinRoom, error, errorParams, responseData);
    }

    private (byte[] Buffer, int Length) HandleCreateRoom(byte[] data, int dataLength)
    {
        CreateRoom request;
        try { request = CreateRoom.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return WriteResponse(MessageType.CreateRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleCreateRoom(request.MaxPlayers);
        byte[] responseData = response?.ToByteArray() ?? [];
        return WriteResponse(MessageType.CreateRoom, error, errorParams, responseData);
    }

    private (byte[]? Buffer, int Length) HandleMoveRequest(byte[] data, int dataLength, ClientSession session)
    {
        if (session.PlayerId == null || session.Room == null)
            return WriteResponse(MessageType.MoveRequest, ErrorCode.PlayerNotInRoom, [], []);

        MoveRequest request;
        try { request = MoveRequest.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
        catch (InvalidProtocolBufferException)
        {
            return WriteResponse(MessageType.MoveRequest, ErrorCode.InvalidInput, [], []);
        }

        session.Room.EnqueueMoveInput(session.PlayerId, new Vec2(request.TargetX, request.TargetY));
        return (null, 0); // no immediate response
    }

    private static (byte[] Buffer, int Length) WriteResponse(MessageType type, ErrorCode error, int[] errorParams, byte[] data) =>
        PacketWriter.WriteServerPacket(type, error, errorParams, data);
}
```

- [ ] **Step 2: Refactor TcpServer**

Replace `Server/StateSync.Server/Network/TcpServer.cs`:

```csharp
namespace StateSync.Server.Network;

using System.Buffers;
using System.Net;
using System.Net.Sockets;

public class TcpServer
{
    private readonly TcpListener _listener;
    private readonly MessageDispatcher _dispatcher;

    public TcpServer(int port, MessageDispatcher dispatcher)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _dispatcher = dispatcher;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _listener.Start();
        Console.WriteLine($"Listening on port {((IPEndPoint)_listener.LocalEndpoint).Port}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            var session = new ClientSession(stream);
            byte[] headerBuf = new byte[8];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var (type, data, dataLength) = await PacketReader.ReadClientPacketAsync(stream, headerBuf);
                    try
                    {
                        var (response, responseLength) = _dispatcher.Dispatch(type, data, dataLength, session);
                        if (response != null)
                        {
                            try
                            {
                                await stream.WriteAsync(response.AsMemory(0, responseLength), ct);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(response);
                            }
                        }
                    }
                    finally
                    {
                        if (dataLength > 0) ArrayPool<byte>.Shared.Return(data);
                    }
                }
            }
            catch (EndOfStreamException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"Client error: {ex.Message}"); }
            finally
            {
                session.Room?.RemoveSession(session);
            }
        }
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

- [ ] **Step 4: Run all tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 5: Commit**

```
git add Server/StateSync.Server/Network/TcpServer.cs Server/StateSync.Server/Network/MessageDispatcher.cs
git commit -m "refactor: TcpServer uses ClientSession, supports push and MoveRequest queueing"
```

---

## Task 9: Implement Room Tick Loop with GameLoop Integration

Wire up the GameLoop to process inputs, tick MoveController, and broadcast messages from Program.cs.

**Files:**
- Create: `Server/StateSync.Server/Game/RoomLoop.cs`
- Modify: `Server/StateSync.Server/Program.cs`

- [ ] **Step 1: Create RoomLoop**

Create `Server/StateSync.Server/Game/RoomLoop.cs` — drives a single room's game loop on a background thread:

```csharp
namespace StateSync.Server.Game;

using Google.Protobuf;
using StateSync.Shared;

public class RoomLoop
{
    private readonly Room _room;
    private readonly GameLoop _gameLoop;

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
        ProcessInputs();
        _room.MoveController.Tick(_gameLoop.DeltaTime);
        BroadcastPositionSync();
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
}
```

- [ ] **Step 2: Update Program.cs**

Replace `Server/StateSync.Server/Program.cs`:

```csharp
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Network;

// Load NavMesh — for now use a simple hardcoded square mesh for development.
// Replace with NavMeshAsset.Load(File.ReadAllText("path/to/navmesh.json")).Build() in production.
var vertices = new Vec2[]
{
    new(0f, 0f), new(100f, 0f), new(100f, 100f), new(0f, 100f)
};
var triangles = new NavTriangle[]
{
    new(0, 1, 2, 1, -1, -1),
    new(0, 2, 3, -1, -1, 0)
};
var navMesh = new NavMesh2D(vertices, triangles);

var roomManager = new RoomManager(navMesh);
roomManager.CreateRoom("room1");

var room = roomManager.GetRoom("room1")!;
var roomLoop = new RoomLoop(room);

var dispatcher = new MessageDispatcher(roomManager);
var server = new TcpServer(7777, dispatcher);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// Start room loop and server concurrently
await Task.WhenAll(
    roomLoop.RunAsync(cts.Token),
    server.StartAsync(cts.Token)
);
```

- [ ] **Step 3: Verify build and run**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

Run: `dotnet run --project Server/StateSync.Server` (then Ctrl+C to stop)
Expected: "Listening on port 7777" output, clean shutdown.

- [ ] **Step 4: Commit**

```
git add Server/StateSync.Server/Game/RoomLoop.cs Server/StateSync.Server/Program.cs
git commit -m "feat: wire up RoomLoop to drive GameLoop, MoveController, and broadcasting"
```

---

## Task 10: Client Movement Controllers

Implement LocalPlayerController and RemotePlayerController for Unity client.

**Files:**
- Create: `Client/Assets/Scripts/Movement/LocalPlayerController.cs`
- Create: `Client/Assets/Scripts/Movement/RemotePlayerController.cs`

- [ ] **Step 1: Create LocalPlayerController**

Create `Client/Assets/Scripts/Movement/LocalPlayerController.cs`:

```csharp
using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Shared;
using StateSync.Client.Network;

namespace StateSync.Client.Movement
{
    public class LocalPlayerController
    {
        private const float SnapThreshold = 0.1f;
        private const float SmoothThreshold = 2.0f;
        private const float CorrectionRate = 0.3f;

        private readonly NetworkManager _network;
        private readonly NavMesh2D _mesh;

        private Vec2[] _path = System.Array.Empty<Vec2>();
        private int _pathIndex;

        public Vec2 Position { get; private set; }
        public float Speed { get; set; }
        public Vec2 Target { get; private set; }
        public bool IsMoving { get; private set; }

        public LocalPlayerController(NetworkManager network, NavMesh2D mesh, Vec2 startPos, float speed)
        {
            _network = network;
            _mesh = mesh;
            Position = startPos;
            Speed = speed;
        }

        public void OnClickMove(Vec2 targetPos)
        {
            var corridor = AStarSolver.Solve(_mesh, Position, targetPos);
            if (corridor == null)
                return;

            var waypoints = FunnelSolver.Solve(_mesh, corridor, Position, targetPos);
            _path = ToArray(waypoints);
            _pathIndex = 0;
            Target = targetPos;
            IsMoving = true;

            _network.Send(MessageType.MoveRequest, new MoveRequest
            {
                TargetX = targetPos.X,
                TargetY = targetPos.Z
            });
        }

        public void Tick(float deltaTime)
        {
            if (!IsMoving)
                return;

            float remaining = Speed * deltaTime;
            while (remaining > 0f && _pathIndex < _path.Length - 1)
            {
                Vec2 next = _path[_pathIndex + 1];
                float dist = Position.DistanceTo(next);

                if (remaining >= dist)
                {
                    Position = next;
                    _pathIndex++;
                    remaining -= dist;
                }
                else
                {
                    Vec2 direction = (next - Position).Normalized();
                    Position = Position + direction * remaining;
                    remaining = 0f;
                }
            }

            if (_pathIndex >= _path.Length - 1)
                IsMoving = false;
        }

        public void OnPositionSync(PositionSync sync)
        {
            var serverPos = new Vec2(sync.PosX, sync.PosY);
            float distance = Position.DistanceTo(serverPos);

            if (distance < SnapThreshold)
                return;

            if (distance < SmoothThreshold)
            {
                float t = CorrectionRate;
                Position = new Vec2(
                    Position.X + (serverPos.X - Position.X) * t,
                    Position.Z + (serverPos.Z - Position.Z) * t
                );
            }
            else
            {
                Position = serverPos;
                if (IsMoving)
                {
                    var corridor = AStarSolver.Solve(_mesh, Position, Target);
                    if (corridor != null)
                    {
                        var waypoints = FunnelSolver.Solve(_mesh, corridor, Position, Target);
                        _path = ToArray(waypoints);
                        _pathIndex = 0;
                    }
                    else
                    {
                        IsMoving = false;
                    }
                }
            }

            if (!sync.IsMoving)
                IsMoving = false;
        }

        private static Vec2[] ToArray(System.Collections.Generic.IReadOnlyList<Vec2> list)
        {
            var arr = new Vec2[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = list[i];
            return arr;
        }
    }
}
```

- [ ] **Step 2: Create RemotePlayerController**

Create `Client/Assets/Scripts/Movement/RemotePlayerController.cs`:

```csharp
using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Shared;

namespace StateSync.Client.Movement
{
    public class RemotePlayerController
    {
        private const float SnapThreshold = 0.1f;
        private const float SmoothThreshold = 2.0f;
        private const float CorrectionRate = 0.3f;

        private readonly NavMesh2D _mesh;

        private Vec2[] _path = System.Array.Empty<Vec2>();
        private int _pathIndex;

        public string PlayerId { get; }
        public Vec2 Position { get; private set; }
        public float Speed { get; private set; }
        public Vec2 Target { get; private set; }
        public bool IsMoving { get; private set; }

        public RemotePlayerController(string playerId, NavMesh2D mesh, Vec2 startPos)
        {
            PlayerId = playerId;
            _mesh = mesh;
            Position = startPos;
        }

        public void OnMoveEvent(MoveEvent evt)
        {
            Target = new Vec2(evt.TargetX, evt.TargetY);
            Speed = evt.Speed;

            var corridor = AStarSolver.Solve(_mesh, Position, Target);
            if (corridor == null)
                return;

            var waypoints = FunnelSolver.Solve(_mesh, corridor, Position, Target);
            _path = ToArray(waypoints);
            _pathIndex = 0;
            IsMoving = true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsMoving)
                return;

            float remaining = Speed * deltaTime;
            while (remaining > 0f && _pathIndex < _path.Length - 1)
            {
                Vec2 next = _path[_pathIndex + 1];
                float dist = Position.DistanceTo(next);

                if (remaining >= dist)
                {
                    Position = next;
                    _pathIndex++;
                    remaining -= dist;
                }
                else
                {
                    Vec2 direction = (next - Position).Normalized();
                    Position = Position + direction * remaining;
                    remaining = 0f;
                }
            }

            if (_pathIndex >= _path.Length - 1)
                IsMoving = false;
        }

        public void OnPositionSync(PositionSync sync)
        {
            var serverPos = new Vec2(sync.PosX, sync.PosY);
            float distance = Position.DistanceTo(serverPos);

            if (distance < SnapThreshold)
                return;

            if (distance < SmoothThreshold)
            {
                float t = CorrectionRate;
                Position = new Vec2(
                    Position.X + (serverPos.X - Position.X) * t,
                    Position.Z + (serverPos.Z - Position.Z) * t
                );
            }
            else
            {
                Position = serverPos;
                if (IsMoving)
                {
                    var corridor = AStarSolver.Solve(_mesh, Position, Target);
                    if (corridor != null)
                    {
                        var waypoints = FunnelSolver.Solve(_mesh, corridor, Position, Target);
                        _path = ToArray(waypoints);
                        _pathIndex = 0;
                    }
                    else
                    {
                        IsMoving = false;
                    }
                }
            }

            if (!sync.IsMoving)
                IsMoving = false;
        }

        private static Vec2[] ToArray(System.Collections.Generic.IReadOnlyList<Vec2> list)
        {
            var arr = new Vec2[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = list[i];
            return arr;
        }
    }
}
```

- [ ] **Step 3: Commit**

```
git add Client/Assets/Scripts/Movement/LocalPlayerController.cs Client/Assets/Scripts/Movement/RemotePlayerController.cs
git commit -m "feat: implement LocalPlayerController and RemotePlayerController with prediction and correction"
```

---

## Task 11: Wire Client Network Handlers for MoveEvent and PositionSync

Register handlers in client NetworkManager to dispatch MoveEvent and PositionSync to the appropriate controllers.

**Files:**
- Create: `Client/Assets/Scripts/Movement/MovementManager.cs`

- [ ] **Step 1: Create MovementManager**

Create `Client/Assets/Scripts/Movement/MovementManager.cs` — coordinates between network events and controllers:

```csharp
using System;
using System.Collections.Generic;
using Pathfinding.Data;
using StateSync.Client.Network;
using StateSync.Shared;

namespace StateSync.Client.Movement
{
    public class MovementManager : IDisposable
    {
        private readonly NetworkManager _network;
        private readonly NavMesh2D _mesh;

        private LocalPlayerController _localPlayer;
        private readonly Dictionary<string, RemotePlayerController> _remotePlayers = new();
        private string _localPlayerId;

        public LocalPlayerController LocalPlayer => _localPlayer;
        public IReadOnlyDictionary<string, RemotePlayerController> RemotePlayers => _remotePlayers;

        public MovementManager(NetworkManager network, NavMesh2D mesh)
        {
            _network = network;
            _mesh = mesh;
        }

        public void Initialize(string localPlayerId, Vec2 startPos, float speed)
        {
            _localPlayerId = localPlayerId;
            _localPlayer = new LocalPlayerController(_network, _mesh, startPos, speed);

            _network.RegisterHandler<MoveEvent>(MessageType.MoveEvent, OnMoveEvent);
            _network.RegisterHandler<PositionSync>(MessageType.PositionSync, OnPositionSync);
        }

        public void AddRemotePlayer(string playerId, Vec2 startPos)
        {
            if (playerId == _localPlayerId)
                return;
            _remotePlayers[playerId] = new RemotePlayerController(playerId, _mesh, startPos);
        }

        public void RemoveRemotePlayer(string playerId)
        {
            _remotePlayers.Remove(playerId);
        }

        public void Tick(float deltaTime)
        {
            _localPlayer?.Tick(deltaTime);
            foreach (var remote in _remotePlayers.Values)
                remote.Tick(deltaTime);
        }

        public void Dispose()
        {
            _network.UnregisterHandler(MessageType.MoveEvent);
            _network.UnregisterHandler(MessageType.PositionSync);
        }

        private void OnMoveEvent(MoveEvent evt, ErrorCode error, int[] errorParams)
        {
            if (error != ErrorCode.Success)
                return;

            if (evt.PlayerId == _localPlayerId)
            {
                _localPlayer.Speed = evt.Speed;
                return;
            }

            if (_remotePlayers.TryGetValue(evt.PlayerId, out var remote))
                remote.OnMoveEvent(evt);
        }

        private void OnPositionSync(PositionSync sync, ErrorCode error, int[] errorParams)
        {
            if (error != ErrorCode.Success)
                return;

            if (sync.PlayerId == _localPlayerId)
            {
                _localPlayer?.OnPositionSync(sync);
                return;
            }

            if (_remotePlayers.TryGetValue(sync.PlayerId, out var remote))
                remote.OnPositionSync(sync);
        }
    }
}
```

- [ ] **Step 2: Commit**

```
git add Client/Assets/Scripts/Movement/MovementManager.cs
git commit -m "feat: implement MovementManager to wire network events to movement controllers"
```

---

## Task 12: Integrate into WorldManager

Wire the MovementManager into the Unity game loop.

**Files:**
- Modify: `Client/Assets/Scripts/World/WorldManager.cs`

- [ ] **Step 1: Update WorldManager**

Replace `Client/Assets/Scripts/World/WorldManager.cs`:

```csharp
using StateSync.Client.Movement;
using StateSync.Client.Network;
using StateSync.Client.UI;
using UnityEngine;

namespace StateSync.Client.World
{
    public sealed class WorldManager : MonoBehaviour
    {
        private static WorldManager _Instance;

        private NetworkManager _NetworkManager;
        private UIManager _UIManager;
        private MovementManager _MovementManager;

        public NetworkManager Network => _NetworkManager;
        public UIManager UI => _UIManager;
        public MovementManager Movement => _MovementManager;

        private void Awake()
        {
            if (_Instance != null && _Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _Instance = this;
            DontDestroyOnLoad(gameObject);

            _NetworkManager = new NetworkManager();
            _NetworkManager.Initialize();

            _UIManager = new UIManager(_NetworkManager);
            _UIManager.Initialize();
        }

        public void InitializeMovement(Pathfinding.Data.NavMesh2D mesh, string localPlayerId, Pathfinding.Data.Vec2 startPos, float speed)
        {
            _MovementManager = new MovementManager(_NetworkManager, mesh);
            _MovementManager.Initialize(localPlayerId, startPos, speed);
        }

        private void Update()
        {
            _NetworkManager?.Tick();
            _UIManager?.Tick(Time.deltaTime);
            _MovementManager?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_Instance == this)
            {
                _Instance = null;
            }

            _MovementManager?.Dispose();
            _MovementManager = null;

            _UIManager?.Dispose();
            _UIManager = null;

            _NetworkManager?.Dispose();
            _NetworkManager = null;
        }
    }
}
```

- [ ] **Step 2: Commit**

```
git add Client/Assets/Scripts/World/WorldManager.cs
git commit -m "feat: integrate MovementManager into WorldManager game loop"
```

---

## Task 13: Final Integration Test

Verify the full server pipeline works end-to-end.

**Files:**
- Test: `Server/StateSync.Server.Tests/Game/RoomLoopTests.cs`

- [ ] **Step 1: Write integration test**

Create `Server/StateSync.Server.Tests/Game/RoomLoopTests.cs`:

```csharp
namespace StateSync.Server.Tests.Game;

using Xunit;
using StateSync.Server.Game;
using StateSync.Shared;
using Pathfinding.Data;
using Google.Protobuf;

public class RoomLoopTests
{
    private static NavMesh2D BuildMesh()
    {
        var vertices = new Vec2[]
        {
            new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f)
        };
        var triangles = new NavTriangle[]
        {
            new(0, 1, 2, 1, -1, -1),
            new(0, 2, 3, -1, -1, 0)
        };
        return new NavMesh2D(vertices, triangles);
    }

    [Fact]
    public void Tick_WithMoveInput_BroadcastsMoveEventAndPositionSync()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh);
        room.TryAdd("player1");
        var sent = new List<(MessageType Type, byte[] Data)>();
        var fakeSession = new FakeSession(sent);
        room.AddSession(fakeSession);

        room.EnqueueMoveInput("player1", new Vec2(5f, 5f));
        var roomLoop = new RoomLoop(room);
        roomLoop.Tick();

        Assert.True(sent.Count >= 2);

        var moveEvent = MoveEvent.Parser.ParseFrom(sent[0].Data);
        Assert.Equal("player1", moveEvent.PlayerId);
        Assert.Equal(5f, moveEvent.TargetX, precision: 3);
        Assert.Equal(5f, moveEvent.TargetY, precision: 3);
        Assert.Equal(MessageType.MoveEvent, sent[0].Type);

        var posSync = PositionSync.Parser.ParseFrom(sent[1].Data);
        Assert.Equal("player1", posSync.PlayerId);
        Assert.True(posSync.IsMoving);
        Assert.Equal(MessageType.PositionSync, sent[1].Type);
    }

    [Fact]
    public void MultipleTicks_EntityReachesDestination_StopsBroadcasting()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh, defaultSpeed: 100f);
        room.TryAdd("player1");
        var sent = new List<(MessageType Type, byte[] Data)>();
        var fakeSession = new FakeSession(sent);
        room.AddSession(fakeSession);

        room.EnqueueMoveInput("player1", new Vec2(1f, 1f));
        var roomLoop = new RoomLoop(room);
        roomLoop.Tick();

        sent.Clear();
        roomLoop.Tick(); // entity should have arrived (speed=100, dt=0.1, dist~1.4)

        // No PositionSync if no moving entities
        Assert.DoesNotContain(sent, m => m.Type == MessageType.PositionSync);
    }

    private class FakeSession : ClientSession
    {
        private readonly List<(MessageType, byte[])> _log;

        public FakeSession(List<(MessageType, byte[])> log) : base(null!)
        {
            _log = log;
        }

        public new void Send(MessageType type, byte[] data) => _log.Add((type, data));
    }
}
```

Note: The FakeSession inherits from ClientSession but overrides Send. If this doesn't work due to the `new` keyword hiding, we need to make `IClientSession` the type used in Room's session list. Adjust Room to use `IClientSession` instead of `ClientSession`:

- [ ] **Step 2: Update Room to use IClientSession**

In `Server/StateSync.Server/Game/Room.cs`, change session list type:

Replace `private readonly List<ClientSession> _sessions = [];` with:
```csharp
private readonly List<IClientSession> _sessions = [];
```

Replace `public IReadOnlyList<ClientSession> Sessions` with:
```csharp
public IReadOnlyList<IClientSession> Sessions { get { lock (_lock) return [.. _sessions]; } }
```

Replace `AddSession(ClientSession session)` and `RemoveSession(ClientSession session)` with:
```csharp
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
```

Update the FakeSession in the test to directly implement `IClientSession`:

```csharp
private class FakeSession : IClientSession
{
    private readonly List<(MessageType, byte[])> _log;
    public FakeSession(List<(MessageType, byte[])> log) => _log = log;
    public void Send(MessageType type, byte[] data) => _log.Add((type, data));
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All tests PASS including the new integration tests.

- [ ] **Step 4: Commit**

```
git add Server/StateSync.Server.Tests/Game/RoomLoopTests.cs Server/StateSync.Server/Game/Room.cs
git commit -m "test: add RoomLoop integration tests verifying MoveEvent and PositionSync broadcast"
```

---

## Summary of Dependencies

```
Task 1 (Vec2.Normalized) ← Task 3 (PlayerEntity)
Task 2 (Proto) ← Task 3, Task 8, Task 9, Task 10, Task 11
Task 3 (PlayerEntity) ← Task 4 (MoveController)
Task 4 (MoveController) ← Task 7 (Room refactor)
Task 5 (GameLoop) ← Task 9 (RoomLoop)
Task 6 (ClientSession) ← Task 7, Task 8
Task 7 (Room) ← Task 8, Task 9
Task 8 (TcpServer refactor) ← Task 9
Task 9 (RoomLoop) ← Task 13 (integration test)
Task 10 (Client controllers) ← Task 11 (MovementManager)
Task 11 (MovementManager) ← Task 12 (WorldManager)
```

Tasks 1 and 2 can be done in parallel. Tasks 10-12 (client) are independent of tasks 5-9 (server) after proto generation.
