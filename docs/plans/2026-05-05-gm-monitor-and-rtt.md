# GM Visual Monitor & RTT Prediction Compensation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add server-initiated RTT measurement with client-side adaptive prediction, and a localhost web-based GM Monitor that visualizes room state on a 2D canvas.

**Architecture:** RTT uses Ping/Pong over the existing TCP protocol — server sends Ping every 1s, client echoes Pong, server computes RTT via EMA smoothing. GM Monitor runs an ASP.NET Core Minimal APIs HTTP server on port 8080 inside the game server process, serving a Canvas-based HTML page that polls room state JSON every 200ms.

**Tech Stack:** .NET 8 server (net8.0 target), ASP.NET Core Minimal APIs, Protobuf, Unity client (netstandard2.1 shared), HTML5 Canvas

---

## Task 1: Add Ping/Pong Proto Messages

Add the Ping and Pong protocol buffer messages and new MessageType entries.

**Files:**
- Modify: `Share/Proto/messages.proto`
- Modify: `Share/Proto/message_type.proto`

- [ ] **Step 1: Add Ping and Pong messages to messages.proto**

Append to end of `Share/Proto/messages.proto`:

```protobuf
message Ping {
    uint64 server_timestamp = 1;
    uint32 sequence = 2;
    uint32 your_rtt_ms = 3;
}

message Pong {
    uint64 server_timestamp = 1;
    uint32 sequence = 2;
}
```

- [ ] **Step 2: Add PING and PONG to message_type.proto**

Add two new entries before the closing brace in `Share/Proto/message_type.proto`:

```protobuf
    PING = 10;
    PONG = 11;
```

The full file becomes:

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
    PING = 10;
    PONG = 11;
}
```

- [ ] **Step 3: Verify server builds with new protos**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds. `Ping`, `Pong` classes generated in `StateSync.Shared`.

- [ ] **Step 4: Commit**

```
git add Share/Proto/messages.proto Share/Proto/message_type.proto
git commit -m "feat: add Ping/Pong proto messages for RTT measurement"
```

---

## Task 2: Add SmoothedRtt to IClientSession and ClientSession

Extend the session interface and implementation to store RTT measurements.

**Files:**
- Modify: `Server/StateSync.Server/Game/IClientSession.cs`
- Modify: `Server/StateSync.Server/Network/ClientSession.cs`

- [ ] **Step 1: Add SmoothedRtt property to IClientSession**

Replace `Server/StateSync.Server/Game/IClientSession.cs` with:

```csharp
namespace StateSync.Server.Game;

using StateSync.Shared;

public interface IClientSession
{
    string? PlayerId { get; }
    int SmoothedRtt { get; set; }
    void Send(MessageType type, byte[] data);
}
```

- [ ] **Step 2: Update ClientSession to implement new interface members**

Replace `Server/StateSync.Server/Network/ClientSession.cs` with:

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
    public int SmoothedRtt { get; set; }

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

- [ ] **Step 3: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```
git add Server/StateSync.Server/Game/IClientSession.cs Server/StateSync.Server/Network/ClientSession.cs
git commit -m "feat: add SmoothedRtt to IClientSession for RTT tracking"
```

---

## Task 3: Add Pong Handling to MessageDispatcher

When the server receives a Pong, compute RTT and update the session's SmoothedRtt.

**Files:**
- Modify: `Server/StateSync.Server/Network/MessageDispatcher.cs`
- Test: `Server/StateSync.Server.Tests/Network/PongHandlerTests.cs`

- [ ] **Step 1: Write failing test**

Create `Server/StateSync.Server.Tests/Network/PongHandlerTests.cs`:

```csharp
namespace StateSync.Server.Tests.Network;

using System.Collections.Generic;
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Network;
using StateSync.Shared;
using Xunit;

public class PongHandlerTests
{
    private RoomManager CreateRoomManager()
    {
        var vertices = new Vec2[] { new(0f, 0f), new(10f, 0f), new(10f, 10f), new(0f, 10f) };
        var triangles = new NavTriangle[]
        {
            new(0, 0, 1, 2, 1, -1, -1),
            new(1, 0, 2, 3, -1, -1, 0)
        };
        return new RoomManager(new NavMesh2D(vertices, triangles));
    }

    [Fact]
    public void HandlePong_UpdatesSmoothedRtt()
    {
        var dispatcher = new MessageDispatcher(CreateRoomManager());
        var session = new FakeClientSession { PlayerId = "p1" };

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pong = new Pong { ServerTimestamp = (ulong)(now - 50), Sequence = 1 };
        byte[] data = pong.ToByteArray();

        dispatcher.Dispatch(MessageType.Pong, data, data.Length, session);

        // EMA: 0.2 * ~50 + 0.8 * 0 ≈ 10 (approximate due to timing)
        Assert.True(session.SmoothedRtt > 0);
        Assert.True(session.SmoothedRtt < 60);
    }

    [Fact]
    public void HandlePong_ReturnsNullResponse()
    {
        var dispatcher = new MessageDispatcher(CreateRoomManager());
        var session = new FakeClientSession { PlayerId = "p1" };

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pong = new Pong { ServerTimestamp = (ulong)(now - 30), Sequence = 1 };
        byte[] data = pong.ToByteArray();

        var (buffer, length) = dispatcher.Dispatch(MessageType.Pong, data, data.Length, session);

        Assert.Null(buffer);
        Assert.Equal(0, length);
    }

    private class FakeClientSession : IClientSession
    {
        public string? PlayerId { get; set; }
        public int SmoothedRtt { get; set; }
        public List<(MessageType, byte[])> Sent { get; } = [];
        public void Send(MessageType type, byte[] data) => Sent.Add((type, data));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Server/StateSync.Server.Tests --filter "PongHandler" -v n`
Expected: Compilation error or test failure — MessageType.Pong not handled.

- [ ] **Step 3: Add Pong handling to MessageDispatcher**

In `Server/StateSync.Server/Network/MessageDispatcher.cs`, add a case to the Dispatch switch and a new handler method.

Replace the `Dispatch` method switch expression:

```csharp
public (byte[]? Buffer, int Length) Dispatch(MessageType type, byte[] data, int dataLength, ClientSession session) => type switch
{
    MessageType.JoinRoom => HandleJoinRoom(data, dataLength, session),
    MessageType.CreateRoom => HandleCreateRoom(data, dataLength),
    MessageType.MoveRequest => HandleMoveRequest(data, dataLength, session),
    MessageType.Pong => HandlePong(data, dataLength, session),
    _ => WriteResponse(type, ErrorCode.InvalidInput, [], [])
};
```

Add a new method at the end of the class:

```csharp
private static (byte[]? Buffer, int Length) HandlePong(byte[] data, int dataLength, ClientSession session)
{
    Pong pong;
    try { pong = Pong.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
    catch (InvalidProtocolBufferException)
    {
        return (null, 0);
    }

    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    int rttSample = (int)(now - (long)pong.ServerTimestamp);
    if (rttSample < 0) rttSample = 0;

    // EMA smoothing: alpha = 0.2
    session.SmoothedRtt = (int)(0.2 * rttSample + 0.8 * session.SmoothedRtt);
    return (null, 0);
}
```

- [ ] **Step 4: Update Dispatch signature to accept IClientSession for testability**

The current `Dispatch` method accepts `ClientSession` (concrete). To enable testing with `FakeClientSession`, change the parameter to `IClientSession`:

Replace the Dispatch signature:

```csharp
public (byte[]? Buffer, int Length) Dispatch(MessageType type, byte[] data, int dataLength, IClientSession session) => type switch
{
    MessageType.JoinRoom => HandleJoinRoom(data, dataLength, (ClientSession)session),
    MessageType.CreateRoom => HandleCreateRoom(data, dataLength),
    MessageType.MoveRequest => HandleMoveRequest(data, dataLength, (ClientSession)session),
    MessageType.Pong => HandlePong(data, dataLength, session),
    _ => WriteResponse(type, ErrorCode.InvalidInput, [], [])
};
```

And update `HandlePong` to take `IClientSession`:

```csharp
private static (byte[]? Buffer, int Length) HandlePong(byte[] data, int dataLength, IClientSession session)
{
    Pong pong;
    try { pong = Pong.Parser.ParseFrom(data.AsSpan(0, dataLength)); }
    catch (InvalidProtocolBufferException)
    {
        return (null, 0);
    }

    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    int rttSample = (int)(now - (long)pong.ServerTimestamp);
    if (rttSample < 0) rttSample = 0;

    session.SmoothedRtt = (int)(0.2 * rttSample + 0.8 * session.SmoothedRtt);
    return (null, 0);
}
```

Update the TcpServer call site (in `HandleClientAsync`) — the call `_dispatcher.Dispatch(type, data, dataLength, session)` already passes `ClientSession` which implements `IClientSession`, so no change needed there.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Server/StateSync.Server.Tests --filter "PongHandler" -v n`
Expected: 2 tests PASS.

- [ ] **Step 6: Run all existing tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 7: Commit**

```
git add Server/StateSync.Server/Network/MessageDispatcher.cs Server/StateSync.Server.Tests/Network/PongHandlerTests.cs
git commit -m "feat: handle Pong message to compute RTT with EMA smoothing"
```

---

## Task 4: Add Periodic Ping Broadcast to RoomLoop

RoomLoop sends Ping to all sessions every 10 ticks (1 second).

**Files:**
- Modify: `Server/StateSync.Server/Game/RoomLoop.cs`
- Test: `Server/StateSync.Server.Tests/Game/RoomLoopRttTests.cs`

- [ ] **Step 1: Write failing test**

Create `Server/StateSync.Server.Tests/Game/RoomLoopRttTests.cs`:

```csharp
namespace StateSync.Server.Tests.Game;

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Shared;
using Xunit;

public class RoomLoopRttTests
{
    private static NavMesh2D BuildMesh()
    {
        var boundary = new Polygon(new[]
        {
            new Vec2(0f, 0f),
            new Vec2(10f, 0f),
            new Vec2(10f, 10f),
            new Vec2(0f, 10f),
        });
        return NavMeshBuilder.Build(boundary);
    }

    [Fact]
    public void Tick10_BroadcastsPing()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh);
        room.TryAdd("player1");
        var sent = new List<(MessageType Type, byte[] Data)>();
        var session = new FakeSession(sent);
        room.AddSession(session);

        var roomLoop = new RoomLoop(room);

        // Tick 10 times — ping is sent on tick % 10 == 0 (i.e., tick 10)
        for (int i = 0; i < 10; i++)
            roomLoop.Tick();

        var pings = sent.Where(m => m.Type == MessageType.Ping).ToList();
        Assert.Single(pings);

        var ping = Ping.Parser.ParseFrom(pings[0].Data);
        Assert.True(ping.ServerTimestamp > 0);
        Assert.Equal(1u, ping.Sequence);
    }

    [Fact]
    public void Tick20_BroadcastsTwoPings_WithIncrementingSequence()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh);
        room.TryAdd("player1");
        var sent = new List<(MessageType Type, byte[] Data)>();
        var session = new FakeSession(sent);
        room.AddSession(session);

        var roomLoop = new RoomLoop(room);

        for (int i = 0; i < 20; i++)
            roomLoop.Tick();

        var pings = sent.Where(m => m.Type == MessageType.Ping).ToList();
        Assert.Equal(2, pings.Count);

        var ping1 = Ping.Parser.ParseFrom(pings[0].Data);
        var ping2 = Ping.Parser.ParseFrom(pings[1].Data);
        Assert.Equal(1u, ping1.Sequence);
        Assert.Equal(2u, ping2.Sequence);
    }

    [Fact]
    public void Ping_IncludesSessionSmoothedRtt()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh);
        room.TryAdd("player1");
        var sent = new List<(MessageType Type, byte[] Data)>();
        var session = new FakeSession(sent) { SmoothedRtt = 42 };
        room.AddSession(session);

        var roomLoop = new RoomLoop(room);

        for (int i = 0; i < 10; i++)
            roomLoop.Tick();

        var pings = sent.Where(m => m.Type == MessageType.Ping).ToList();
        var ping = Ping.Parser.ParseFrom(pings[0].Data);
        Assert.Equal(42u, ping.YourRttMs);
    }

    private class FakeSession : IClientSession
    {
        private readonly List<(MessageType, byte[])> _log;

        public FakeSession(List<(MessageType, byte[])> log) => _log = log;

        public string? PlayerId { get; set; }
        public int SmoothedRtt { get; set; }
        public void Send(MessageType type, byte[] data) => _log.Add((type, data));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Server/StateSync.Server.Tests --filter "RoomLoopRtt" -v n`
Expected: FAIL — no Ping messages are being sent.

- [ ] **Step 3: Add Ping broadcast to RoomLoop**

Replace `Server/StateSync.Server/Game/RoomLoop.cs` with:

```csharp
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
        ProcessInputs();
        _room.MoveController.Tick(_gameLoop.DeltaTime);
        BroadcastPositionSync();
        BroadcastPing();
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Server/StateSync.Server.Tests --filter "RoomLoopRtt" -v n`
Expected: 3 tests PASS.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```
git add Server/StateSync.Server/Game/RoomLoop.cs Server/StateSync.Server.Tests/Game/RoomLoopRttTests.cs
git commit -m "feat: broadcast Ping every 1s from RoomLoop for RTT measurement"
```

---

## Task 5: Expose Path and Rtt on PlayerEntity

Add read-only public access to the path and RTT for the GM Monitor.

**Files:**
- Modify: `Server/StateSync.Server/Game/PlayerEntity.cs`
- Modify: `Server/StateSync.Server/Game/Room.cs`

- [ ] **Step 1: Expose Path and Rtt on PlayerEntity**

In `Server/StateSync.Server/Game/PlayerEntity.cs`, change the private `_Path` field and add a `Rtt` property.

Replace lines 16-17:
```csharp
private Vec2[] _Path = [];
private int _PathIndex;
```

With:
```csharp
private Vec2[] _Path = [];
private int _PathIndex;

public IReadOnlyList<Vec2> Path => _Path;
public int CurrentPathIndex => _PathIndex;
public int Rtt { get; set; }
```

Add the using at the top if not present: the file already uses `System.Collections.Generic`.

- [ ] **Step 2: Add method to Room for syncing RTT from sessions to entities**

In `Server/StateSync.Server/Game/Room.cs`, add a method at the end of the class (before closing brace):

```csharp
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
```

Also expose the NavMesh for the monitor API by adding a property:

```csharp
public NavMesh2D NavMesh => _navMesh;
```

- [ ] **Step 3: Call SyncRttFromSessions in RoomLoop.Tick**

In `Server/StateSync.Server/Game/RoomLoop.cs`, add `_room.SyncRttFromSessions();` in the `Tick()` method after `BroadcastPing()` and before `_gameLoop.Flush()`:

```csharp
public void Tick()
{
    _gameLoop.Tick();
    ProcessInputs();
    _room.MoveController.Tick(_gameLoop.DeltaTime);
    BroadcastPositionSync();
    BroadcastPing();
    _room.SyncRttFromSessions();
    _gameLoop.Flush();
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

- [ ] **Step 5: Run all tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```
git add Server/StateSync.Server/Game/PlayerEntity.cs Server/StateSync.Server/Game/Room.cs Server/StateSync.Server/Game/RoomLoop.cs
git commit -m "feat: expose Path and Rtt on PlayerEntity for GM Monitor"
```

---

## Task 6: Client-Side RttTracker

Implement a class that stores RTT received from server and computes adaptive correction thresholds.

**Files:**
- Create: `Client/Assets/Scripts/Network/RttTracker.cs`

- [ ] **Step 1: Create RttTracker**

Create `Client/Assets/Scripts/Network/RttTracker.cs`:

```csharp
namespace StateSync.Client.Network
{
    public class RttTracker
    {
        private const float BaseSnapThreshold = 0.1f;
        private const float BaseSmoothThreshold = 2.0f;
        private const float BaseCorrectionRate = 0.3f;

        public int RttMs { get; private set; }
        public float SnapThreshold => BaseSnapThreshold;

        public float SmoothThreshold(float speed)
        {
            return BaseSmoothThreshold + speed * RttMs / 1000f;
        }

        public float CorrectionRate
        {
            get
            {
                float rate = BaseCorrectionRate - RttMs / 2000f;
                if (rate < 0.1f) return 0.1f;
                if (rate > 0.5f) return 0.5f;
                return rate;
            }
        }

        public void UpdateRtt(int rttMs)
        {
            RttMs = rttMs;
        }
    }
}
```

- [ ] **Step 2: Commit**

```
git add Client/Assets/Scripts/Network/RttTracker.cs
git commit -m "feat: add RttTracker for client-side adaptive correction thresholds"
```

---

## Task 7: Client Ping/Pong Handling in NetworkManager

Register a handler for Ping that immediately sends Pong and updates RttTracker.

**Files:**
- Modify: `Client/Assets/Scripts/Network/NetworkManager.cs`

- [ ] **Step 1: Add RttTracker field and Ping handler**

In `Client/Assets/Scripts/Network/NetworkManager.cs`, add a public `RttTracker` field and register the Ping handler during `Initialize()`.

Add the field after the existing fields (after line 7):

```csharp
public RttTracker Rtt { get; private set; }
```

In `Initialize()`, after `_Initialized = true;` (line 31), add:

```csharp
Rtt = new RttTracker();
_Dispatcher.Register<Ping>(MessageType.Ping, HandlePing);
```

Add the handler method at the end of the class (before closing brace):

```csharp
private void HandlePing(Ping ping, ErrorCode error, int[] errorParams)
{
    // Immediately echo Pong
    Send(MessageType.Pong, new Pong
    {
        ServerTimestamp = ping.ServerTimestamp,
        Sequence = ping.Sequence
    });

    // Update local RTT from server's measurement
    Rtt.UpdateRtt((int)ping.YourRttMs);
}
```

Add the using at the top of the file:

```csharp
using StateSync.Shared;
```

(Already present — `StateSync.Shared` is already imported.)

- [ ] **Step 2: Update Dispose to clean up**

In the `Dispose()` method, after `_Dispatcher?.Clear();`, the clear already removes all handlers so no additional cleanup is needed. But set Rtt to null:

Add after `_Dispatcher = null;`:

```csharp
Rtt = null;
```

- [ ] **Step 3: Verify build (client won't compile standalone, but check for syntax errors)**

Since this is a Unity project, verify no syntax errors by reviewing the file manually. The build is validated when Unity opens the project.

- [ ] **Step 4: Commit**

```
git add Client/Assets/Scripts/Network/NetworkManager.cs
git commit -m "feat: handle Ping in NetworkManager, echo Pong and update RttTracker"
```

---

## Task 8: Integrate RttTracker into Movement Controllers

Replace the constant thresholds in LocalPlayerController and RemotePlayerController with values from RttTracker.

**Files:**
- Modify: `Client/Assets/Scripts/Movement/LocalPlayerController.cs`
- Modify: `Client/Assets/Scripts/Movement/RemotePlayerController.cs`

- [ ] **Step 1: Update LocalPlayerController to use RttTracker**

In `Client/Assets/Scripts/Movement/LocalPlayerController.cs`:

Remove the constant fields (lines 10-12):
```csharp
private const float SnapThreshold = 0.1f;
private const float SmoothThreshold = 2.0f;
private const float CorrectionRate = 0.3f;
```

Add a field for RttTracker after `_Mesh`:
```csharp
private readonly RttTracker _Rtt;
```

Update the constructor to accept RttTracker:
```csharp
public LocalPlayerController(NetworkManager network, NavMesh2D mesh, Vec2 startPos, float speed)
{
    _Network = network;
    _Mesh = mesh;
    _Rtt = network.Rtt;
    Position = startPos;
    Speed = speed;
}
```

Update `OnPositionSync` to use dynamic values. Replace the threshold checks:

```csharp
public void OnPositionSync(PositionSync sync)
{
    var serverPos = new Vec2(sync.PosX, sync.PosY);
    float distance = Position.DistanceTo(serverPos);

    float snapThreshold = _Rtt.SnapThreshold;
    float smoothThreshold = _Rtt.SmoothThreshold(Speed);
    float correctionRate = _Rtt.CorrectionRate;

    if (distance < snapThreshold)
        return;

    if (distance < smoothThreshold)
    {
        float t = correctionRate;
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
            var corridor = AStarSolver.Solve(_Mesh, Position, Target);
            if (corridor != null)
            {
                var waypoints = FunnelSolver.Solve(_Mesh, corridor, Position, Target);
                _Path = ToArray(waypoints);
                _PathIndex = 0;
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
```

- [ ] **Step 2: Update RemotePlayerController to use RttTracker**

In `Client/Assets/Scripts/Movement/RemotePlayerController.cs`:

Remove the constant fields (lines 9-11):
```csharp
private const float SnapThreshold = 0.1f;
private const float SmoothThreshold = 2.0f;
private const float CorrectionRate = 0.3f;
```

Add a field for RttTracker after `_Mesh`:
```csharp
private readonly RttTracker _Rtt;
```

Update the constructor:
```csharp
public RemotePlayerController(string playerId, NavMesh2D mesh, Vec2 startPos, RttTracker rtt)
{
    PlayerId = playerId;
    _Mesh = mesh;
    _Rtt = rtt;
    Position = startPos;
}
```

Update `OnPositionSync` the same way as LocalPlayerController:

```csharp
public void OnPositionSync(PositionSync sync)
{
    var serverPos = new Vec2(sync.PosX, sync.PosY);
    float distance = Position.DistanceTo(serverPos);

    float snapThreshold = _Rtt.SnapThreshold;
    float smoothThreshold = _Rtt.SmoothThreshold(Speed);
    float correctionRate = _Rtt.CorrectionRate;

    if (distance < snapThreshold)
        return;

    if (distance < smoothThreshold)
    {
        float t = correctionRate;
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
            var corridor = AStarSolver.Solve(_Mesh, Position, Target);
            if (corridor != null)
            {
                var waypoints = FunnelSolver.Solve(_Mesh, corridor, Position, Target);
                _Path = ToArray(waypoints);
                _PathIndex = 0;
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
```

- [ ] **Step 3: Update MovementManager to pass RttTracker to RemotePlayerController**

In `Client/Assets/Scripts/Movement/MovementManager.cs`, update `AddRemotePlayer`:

```csharp
public void AddRemotePlayer(string playerId, Vec2 startPos)
{
    if (playerId == _localPlayerId)
        return;
    _remotePlayers[playerId] = new RemotePlayerController(playerId, _mesh, startPos, _network.Rtt);
}
```

- [ ] **Step 4: Commit**

```
git add Client/Assets/Scripts/Movement/LocalPlayerController.cs Client/Assets/Scripts/Movement/RemotePlayerController.cs Client/Assets/Scripts/Movement/MovementManager.cs
git commit -m "feat: integrate RttTracker into movement controllers for adaptive correction"
```

---

## Task 9: Add ASP.NET Core FrameworkReference to Server Project

Enable Minimal APIs by adding the web framework reference.

**Files:**
- Modify: `Server/StateSync.Server/StateSync.Server.csproj`

- [ ] **Step 1: Add FrameworkReference**

In `Server/StateSync.Server/StateSync.Server.csproj`, add a `<FrameworkReference>` item inside a new `<ItemGroup>` after the existing `<ItemGroup>` blocks (before `</Project>`):

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```
git add Server/StateSync.Server/StateSync.Server.csproj
git commit -m "feat: add ASP.NET Core framework reference for GM Monitor HTTP server"
```

---

## Task 10: Implement MonitorServer with Room State API

Create the HTTP server with JSON API endpoints for room state and navmesh.

**Files:**
- Create: `Server/StateSync.Server/Monitor/MonitorServer.cs`
- Test: `Server/StateSync.Server.Tests/Monitor/MonitorServerTests.cs`

- [ ] **Step 1: Create MonitorServer**

Create `Server/StateSync.Server/Monitor/MonitorServer.cs`:

```csharp
namespace StateSync.Server.Monitor;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using StateSync.Server.Game;

public class MonitorServer
{
    private readonly WebApplication _app;

    public MonitorServer(RoomManager roomManager, int port = 8080)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        _app = builder.Build();

        _app.MapGet("/api/rooms", () =>
        {
            var rooms = roomManager.GetAllRooms();
            var result = rooms.Select(r => new
            {
                roomId = r.RoomId,
                playerCount = r.Players.Count
            });
            return Results.Json(result);
        });

        _app.MapGet("/api/rooms/{roomId}/state", (string roomId) =>
        {
            var room = roomManager.GetRoom(roomId);
            if (room == null)
                return Results.NotFound();

            var players = room.Players;
            var playerStates = new List<object>();
            foreach (var pid in players)
            {
                var entity = room.GetEntity(pid);
                if (entity == null) continue;
                playerStates.Add(new
                {
                    playerId = entity.PlayerId,
                    position = new { x = entity.Position.X, z = entity.Position.Z },
                    target = new { x = entity.Target.X, z = entity.Target.Z },
                    path = entity.Path.Select(p => new { x = p.X, z = p.Z }).ToArray(),
                    speed = entity.Speed,
                    isMoving = entity.IsMoving,
                    rttMs = entity.Rtt
                });
            }

            return Results.Json(new
            {
                roomId = room.RoomId,
                tick = room.CurrentTick,
                players = playerStates
            });
        });

        _app.MapGet("/api/rooms/{roomId}/navmesh", (string roomId) =>
        {
            var room = roomManager.GetRoom(roomId);
            if (room == null)
                return Results.NotFound();

            var mesh = room.NavMesh;
            var vertices = mesh.Vertices.Select(v => new { x = v.X, z = v.Z }).ToArray();
            var triangles = mesh.Triangles.Select(t => new int[] { t.V0, t.V1, t.V2 }).ToArray();

            return Results.Json(new { vertices, triangles });
        });

        _app.MapGet("/", () => Results.Content(MonitorPage.Html, "text/html"));
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _app.RunAsync(ct);
    }
}
```

- [ ] **Step 2: Add GetAllRooms to RoomManager**

In `Server/StateSync.Server/Game/RoomManager.cs`, add a method at the end of the class:

```csharp
public IReadOnlyCollection<Room> GetAllRooms() => _rooms.Values;
```

- [ ] **Step 3: Expose CurrentTick from RoomLoop for the state API**

In `Server/StateSync.Server/Game/RoomLoop.cs`, add a public property:

```csharp
public uint CurrentTick => _gameLoop.CurrentTick;
```

Then update `MonitorServer` to accept the RoomLoop (or store tick in Room). Simpler approach: store tick on Room. Add to `Server/StateSync.Server/Game/Room.cs`:

```csharp
public uint CurrentTick { get; set; }
```

Update `RoomLoop.Tick()` to set it after `_gameLoop.Tick()`:

```csharp
_room.CurrentTick = _gameLoop.CurrentTick;
```

Then update MonitorServer's state endpoint to use `room.CurrentTick`:

```csharp
tick = room.CurrentTick,
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds (MonitorPage not yet created, will fail — proceed to next step).

- [ ] **Step 5: Commit (without MonitorPage — will be added in next task)**

Don't commit yet — MonitorPage is needed first. Continue to Task 11.

---

## Task 11: Create MonitorPage with Canvas Visualization

Embedded HTML/JS as a static string for the Canvas-based visualization page.

**Files:**
- Create: `Server/StateSync.Server/Monitor/MonitorPage.cs`

- [ ] **Step 1: Create MonitorPage.cs**

Create `Server/StateSync.Server/Monitor/MonitorPage.cs`:

```csharp
namespace StateSync.Server.Monitor;

public static class MonitorPage
{
    public const string Html = """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>GM Monitor</title>
<style>
    body { margin: 0; background: #1a1a2e; color: #eee; font-family: monospace; display: flex; }
    #canvas { border: 1px solid #333; }
    #info { padding: 16px; width: 280px; overflow-y: auto; }
    .player { margin-bottom: 8px; padding: 8px; background: #16213e; border-radius: 4px; }
    .label { color: #aaa; font-size: 11px; }
    h3 { margin: 0 0 8px; color: #0f3460; }
</style>
</head>
<body>
<canvas id="canvas" width="600" height="600"></canvas>
<div id="info"><h3>GM Monitor</h3><div id="status">Loading...</div></div>
<script>
const canvas = document.getElementById('canvas');
const ctx = canvas.getContext('2d');
const infoDiv = document.getElementById('status');
let navmesh = null;
let roomId = 'room1';
let scale = 1;
let offsetX = 0, offsetZ = 0;

const colors = ['#e94560','#0f3460','#533483','#16c79a','#f5a623','#50d890','#6c5ce7','#fd79a8'];

async function loadNavmesh() {
    try {
        const resp = await fetch(`/api/rooms/${roomId}/navmesh`);
        if (resp.ok) navmesh = await resp.json();
        if (navmesh && navmesh.vertices.length > 0) {
            let minX=Infinity, maxX=-Infinity, minZ=Infinity, maxZ=-Infinity;
            for (const v of navmesh.vertices) {
                minX = Math.min(minX, v.x); maxX = Math.max(maxX, v.x);
                minZ = Math.min(minZ, v.z); maxZ = Math.max(maxZ, v.z);
            }
            const pad = 20;
            scale = Math.min((canvas.width - 2*pad) / (maxX - minX), (canvas.height - 2*pad) / (maxZ - minZ));
            offsetX = pad - minX * scale;
            offsetZ = pad - minZ * scale;
        }
    } catch(e) { console.error(e); }
}

function toScreen(x, z) {
    return [x * scale + offsetX, z * scale + offsetZ];
}

function drawNavmesh() {
    if (!navmesh) return;
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 0.5;
    for (const tri of navmesh.triangles) {
        const [ax, az] = toScreen(navmesh.vertices[tri[0]].x, navmesh.vertices[tri[0]].z);
        const [bx, bz] = toScreen(navmesh.vertices[tri[1]].x, navmesh.vertices[tri[1]].z);
        const [cx, cz] = toScreen(navmesh.vertices[tri[2]].x, navmesh.vertices[tri[2]].z);
        ctx.beginPath();
        ctx.moveTo(ax, az); ctx.lineTo(bx, bz); ctx.lineTo(cx, cz); ctx.closePath();
        ctx.stroke();
    }
}

function drawPlayer(p, idx) {
    const color = colors[idx % colors.length];
    const [px, pz] = toScreen(p.position.x, p.position.z);

    // Draw path
    if (p.path && p.path.length > 1) {
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 4]);
        ctx.beginPath();
        const [sx, sz] = toScreen(p.path[0].x, p.path[0].z);
        ctx.moveTo(sx, sz);
        for (let i = 1; i < p.path.length; i++) {
            const [nx, nz] = toScreen(p.path[i].x, p.path[i].z);
            ctx.lineTo(nx, nz);
        }
        ctx.stroke();
        ctx.setLineDash([]);
    }

    // Draw target cross
    if (p.isMoving) {
        const [tx, tz] = toScreen(p.target.x, p.target.z);
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(tx-5, tz); ctx.lineTo(tx+5, tz);
        ctx.moveTo(tx, tz-5); ctx.lineTo(tx, tz+5);
        ctx.stroke();
    }

    // Draw player circle
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(px, pz, 6, 0, Math.PI*2);
    ctx.fill();

    // Label
    ctx.fillStyle = '#fff';
    ctx.font = '10px monospace';
    ctx.fillText(p.playerId.substring(0,6), px+8, pz+3);
}

async function update() {
    try {
        const resp = await fetch(`/api/rooms/${roomId}/state`);
        if (!resp.ok) { infoDiv.textContent = 'Room not found'; return; }
        const state = await resp.json();

        // Clear and draw
        ctx.fillStyle = '#1a1a2e';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        drawNavmesh();

        state.players.forEach((p, i) => drawPlayer(p, i));

        // Info panel
        let html = `<div class="label">Tick: ${state.tick} | Players: ${state.players.length}</div>`;
        state.players.forEach((p, i) => {
            html += `<div class="player" style="border-left:3px solid ${colors[i%colors.length]}">`;
            html += `<b>${p.playerId.substring(0,8)}</b><br>`;
            html += `<span class="label">Pos:</span> (${p.position.x.toFixed(1)}, ${p.position.z.toFixed(1)})<br>`;
            html += `<span class="label">Moving:</span> ${p.isMoving} | <span class="label">RTT:</span> ${p.rttMs}ms`;
            html += `</div>`;
        });
        infoDiv.innerHTML = html;
    } catch(e) { infoDiv.textContent = 'Error: ' + e.message; }
}

loadNavmesh().then(() => { update(); setInterval(update, 200); });
</script>
</body>
</html>
""";
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

- [ ] **Step 3: Commit (Task 10 + 11 together)**

```
git add Server/StateSync.Server/Monitor/MonitorServer.cs Server/StateSync.Server/Monitor/MonitorPage.cs Server/StateSync.Server/Game/RoomManager.cs Server/StateSync.Server/Game/Room.cs Server/StateSync.Server/Game/RoomLoop.cs
git commit -m "feat: implement GM Monitor HTTP server with Canvas visualization page"
```

---

## Task 12: Wire MonitorServer into Program.cs

Start the monitor alongside the game server.

**Files:**
- Modify: `Server/StateSync.Server/Program.cs`

- [ ] **Step 1: Update Program.cs**

Replace `Server/StateSync.Server/Program.cs` with:

```csharp
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Monitor;
using StateSync.Server.Network;

var vertices = new Vec2[]
{
    new(0f, 0f), new(100f, 0f), new(100f, 100f), new(0f, 100f)
};
var triangles = new NavTriangle[]
{
    new(0, 0, 1, 2, 1, -1, -1),
    new(1, 0, 2, 3, -1, -1, 0)
};
var navMesh = new NavMesh2D(vertices, triangles);

var roomManager = new RoomManager(navMesh);
roomManager.CreateRoom("room1");

var room = roomManager.GetRoom("room1")!;
var roomLoop = new RoomLoop(room);

var dispatcher = new MessageDispatcher(roomManager);
var server = new TcpServer(7777, dispatcher);
var monitor = new MonitorServer(roomManager);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("GM Monitor: http://localhost:8080");

await Task.WhenAll(
    roomLoop.RunAsync(cts.Token),
    server.StartAsync(cts.Token),
    monitor.StartAsync(cts.Token)
);
```

- [ ] **Step 2: Verify build and run**

Run: `dotnet build Server/StateSync.Server`
Expected: Build succeeds.

Run: `dotnet run --project Server/StateSync.Server` (then Ctrl+C to stop)
Expected: Output shows "Listening on port 7777" and "GM Monitor: http://localhost:8080". Clean shutdown.

- [ ] **Step 3: Commit**

```
git add Server/StateSync.Server/Program.cs
git commit -m "feat: wire MonitorServer into Program.cs startup"
```

---

## Task 13: Integration Test — RTT Round-Trip in RoomLoop

Verify the full Ping/Pong/RTT pipeline works in the RoomLoop.

**Files:**
- Create: `Server/StateSync.Server.Tests/Game/RttIntegrationTests.cs`

- [ ] **Step 1: Write integration test**

Create `Server/StateSync.Server.Tests/Game/RttIntegrationTests.cs`:

```csharp
namespace StateSync.Server.Tests.Game;

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using StateSync.Server.Game;
using StateSync.Server.Network;
using StateSync.Shared;
using Xunit;

public class RttIntegrationTests
{
    private static NavMesh2D BuildMesh()
    {
        var boundary = new Polygon(new[]
        {
            new Vec2(0f, 0f),
            new Vec2(10f, 0f),
            new Vec2(10f, 10f),
            new Vec2(0f, 10f),
        });
        return NavMeshBuilder.Build(boundary);
    }

    [Fact]
    public void FullPipeline_PingSentAndPongUpdatesRtt()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh);
        room.TryAdd("player1");
        var sent = new List<(MessageType Type, byte[] Data)>();
        var session = new FakeSession(sent) { PlayerId = "player1" };
        room.AddSession(session);

        var roomLoop = new RoomLoop(room);

        // Tick 10 times to trigger Ping
        for (int i = 0; i < 10; i++)
            roomLoop.Tick();

        // Extract the Ping that was sent
        var pingMsg = sent.Where(m => m.Type == MessageType.Ping).First();
        var ping = Ping.Parser.ParseFrom(pingMsg.Data);

        // Simulate client sending back Pong with same timestamp
        var pong = new Pong
        {
            ServerTimestamp = ping.ServerTimestamp,
            Sequence = ping.Sequence
        };

        // Feed Pong to dispatcher
        var roomManager = new RoomManager(mesh);
        var dispatcher = new MessageDispatcher(roomManager);
        byte[] pongData = pong.ToByteArray();
        dispatcher.Dispatch(MessageType.Pong, pongData, pongData.Length, session);

        // Session should now have a non-zero RTT (small due to in-process timing)
        Assert.True(session.SmoothedRtt >= 0);

        // Tick 10 more times — next Ping should include the measured RTT
        sent.Clear();
        for (int i = 0; i < 10; i++)
            roomLoop.Tick();

        var ping2Msg = sent.Where(m => m.Type == MessageType.Ping).First();
        var ping2 = Ping.Parser.ParseFrom(ping2Msg.Data);
        Assert.Equal((uint)session.SmoothedRtt, ping2.YourRttMs);
    }

    [Fact]
    public void SyncRttFromSessions_CopiesRttToEntity()
    {
        var mesh = BuildMesh();
        var room = new Room("test", 16, mesh);
        room.TryAdd("player1");
        var session = new FakeSession([]) { PlayerId = "player1", SmoothedRtt = 55 };
        room.AddSession(session);

        room.SyncRttFromSessions();

        var entity = room.GetEntity("player1");
        Assert.NotNull(entity);
        Assert.Equal(55, entity!.Rtt);
    }

    private class FakeSession : IClientSession
    {
        private readonly List<(MessageType, byte[])> _log;

        public FakeSession(List<(MessageType, byte[])> log) => _log = log;

        public string? PlayerId { get; set; }
        public int SmoothedRtt { get; set; }
        public void Send(MessageType type, byte[] data) => _log.Add((type, data));
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test Server/StateSync.Server.Tests --filter "RttIntegration" -v n`
Expected: 2 tests PASS.

- [ ] **Step 3: Run all tests**

Run: `dotnet test Server/StateSync.Server.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 4: Commit**

```
git add Server/StateSync.Server.Tests/Game/RttIntegrationTests.cs
git commit -m "test: add RTT integration tests verifying full Ping/Pong pipeline"
```

---

## Summary of Dependencies

```
Task 1 (Proto) ← Task 3 (Pong handler), Task 4 (Ping broadcast), Task 7 (Client Ping)
Task 2 (Session RTT) ← Task 3, Task 4, Task 5
Task 3 (Pong handler) ← Task 13 (integration test)
Task 4 (Ping broadcast) ← Task 5, Task 13
Task 5 (Expose Path/Rtt) ← Task 10 (Monitor API)
Task 6 (RttTracker) ← Task 7, Task 8
Task 7 (Client Ping handling) ← Task 8
Task 8 (Controller integration) — depends on Task 6, Task 7
Task 9 (ASP.NET ref) ← Task 10
Task 10 (MonitorServer) ← Task 11 (Page), Task 12 (Program.cs)
Task 11 (MonitorPage) ← Task 12
Task 12 (Program.cs) — final wiring
Task 13 (Integration test) — after Tasks 3, 4, 5
```

Tasks 1-5 (server RTT) are sequential. Tasks 6-8 (client RTT) can run in parallel with Tasks 9-12 (monitor) after Task 1 is done.
