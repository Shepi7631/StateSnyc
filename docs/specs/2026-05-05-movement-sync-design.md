# Movement Synchronization Design

## Overview

MMO 点击移动同步，权威服务端模式。客户端点击目标点后双端同时运行寻路（共享 Pathfinding.Core），服务端以 10 Hz 推进移动并广播权威位置，客户端本地预测 + 服务端校正。

## Constraints

- 同步模型：权威服务端（Server Authoritative）
- 广播频率：10 Hz（100ms/tick）
- 房间规模：≤16 人，全量广播
- 移动方式：点击移动（click-to-move），双端寻路
- 共享代码：Pathfinding.Core（NavMesh + A* + Funnel）

## Architecture

### Data Flow

```
Client click → [MoveRequest(targetPos)] → Server
Server: 寻路 → [MoveEvent(player_id, target, speed)] → All clients (once)
Server: 每tick推进 → [PositionSync(player_id, pos)] → All clients (periodic)
Client: 收到MoveEvent → 本地寻路并移动 | 收到PositionSync → 校正偏差
```

### Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Local player prediction | Yes | Eliminate RTT delay, instant response on click |
| Remote player rendering | Local prediction (not pure interpolation) | All clients pathfind locally on MoveEvent, same as local player |
| Position correction | Smooth lerp to authoritative position | Avoid visual teleportation |
| Pathfinding location | Both client and server (Pathfinding.Core) | Deterministic consistency |
| Message separation | MoveEvent + PositionSync | Move event triggers prediction; PositionSync corrects drift |

## Protocol

New proto messages in `Share/Proto/messages.proto`:

```protobuf
// Client → Server: player clicked to move
message MoveRequest {
    float target_x = 1;
    float target_y = 2;
}

// Server → All clients: a player started moving to a target
message MoveEvent {
    string player_id = 1;
    float target_x = 2;
    float target_y = 3;
    float speed = 4;
    uint32 tick = 5;        // server tick when movement started
}

// Server → All clients: authoritative position correction
message PositionSync {
    string player_id = 1;
    float pos_x = 2;
    float pos_y = 3;
    bool is_moving = 4;
    uint32 tick = 5;        // server tick this position corresponds to
}
```

New MessageType entries:

```protobuf
MOVE_REQUEST = 7;
MOVE_EVENT = 8;
POSITION_SYNC = 9;
```

Two distinct broadcast types:
- **MoveEvent**: sent once when a player starts moving. All clients receive it and locally pathfind for that player.
- **PositionSync**: sent periodically (10 Hz) for moving players. Used purely for drift correction.

## Server Components

### GameLoop

Room-level fixed-rate loop (10 Hz). GameLoop itself does not decide what to broadcast — modules register outgoing packets each tick, and GameLoop flushes them:

1. Drain input queue (MoveRequest messages)
2. Call `MoveController.Tick(deltaTime)` — MoveController registers PlayerState packets if there are moving entities
3. Flush all registered outgoing packets to their target sessions
4. Clear the outgoing packet queue

Modules (MoveController, future combat module, etc.) call a registration method to enqueue packets:

```
GameLoop.EnqueueBroadcast(sessions, type, data)   // broadcast to a set of sessions
GameLoop.EnqueueSend(session, type, data)          // send to one session
```

This keeps GameLoop generic — it only handles timing and flushing, not domain logic.

### NavMesh Loading

Server loads the same NavMesh JSON asset as client. The NavMesh asset path is configured per-room (or globally for single-map scenarios). Both sides must use identical NavMesh data to guarantee path consistency.

### Player Speed

Movement speed is authoritative server data, owned by PlayerEntity on the server. The client receives speed via PlayerState in PlayerState broadcasts. Client uses the server-provided speed for local prediction — never a locally hardcoded value.

### PlayerEntity

Per-player movement state on server:

```
Fields:
  - PlayerId: string
  - Position: Vec2            // authoritative position
  - Path: Vec2[]              // Funnel output waypoints
  - PathIndex: int            // current segment
  - Speed: float              // movement speed
  - IsMoving: bool

Methods:
  - SetDestination(target: Vec2): bool
      → A* + Funnel on shared NavMesh
      → sets Path, PathIndex=0, IsMoving=true
      → returns false if target not on NavMesh
  
  - Tick(deltaTime: float): void
      → advance position along path by speed*dt
      → set IsMoving=false when path complete
```

### MoveController

Manages the set of actively moving players:

```
Fields:
  - _movingEntities: HashSet<PlayerEntity>

Methods:
  - StartMove(entity, target): void
      → entity.SetDestination(target)
      → add to _movingEntities
  
  - StopMove(entity): void
      → remove from _movingEntities
  
  - Tick(deltaTime): void
      → foreach entity in _movingEntities:
          entity.Tick(deltaTime)
          if (!entity.IsMoving): schedule removal
  
  - HasMovingEntities: bool

  - BroadcastMoveEvent(entity, GameLoop): void
      → called once when a player starts moving
      → builds MoveEvent(player_id, target, speed)
      → registers via GameLoop.EnqueueBroadcast

  - BroadcastPositionSync(GameLoop): void
      → called each tick when HasMovingEntities is true
      → builds one PositionSync per moving entity
      → registers each via GameLoop.EnqueueBroadcast
```

### ClientSession (Network layer refactor)

Current TcpServer uses request-response pattern. Refactor to support:

- **Connection binding**: after JoinRoom, ClientSession associates with playerId + Room
- **Input queue**: MoveRequest enters room's queue, no immediate response
- **Push capability**: GameLoop can send PlayerState to all sessions without a preceding request

```
ClientSession:
  - Stream: NetworkStream
  - PlayerId: string (bound after JoinRoom)
  - Room: Room reference
  
  Methods:
  - SendAsync(type, data): Task    // push message to client
```

### Movement Tick Logic (PlayerEntity.Tick)

```
Tick(float dt):
  if (!IsMoving) return
  
  remainingDist = Speed * dt
  while (remainingDist > 0 && PathIndex < Path.Length - 1):
    nextWaypoint = Path[PathIndex + 1]
    distToNext = Position.DistanceTo(nextWaypoint)
    
    if (remainingDist >= distToNext):
      Position = nextWaypoint
      PathIndex++
      remainingDist -= distToNext
    else:
      direction = (nextWaypoint - Position).Normalized()
      Position += direction * remainingDist
      remainingDist = 0
  
  if (PathIndex >= Path.Length - 1):
    IsMoving = false
```

## Client Components

### LocalPlayerController

Local player with client-side prediction. Does not wait for server response — moves immediately on click, corrects only when PositionSync arrives:

```
Fields:
  - Position: Vec2              // local predicted position
  - Path: Vec2[]                // local pathfinding result
  - PathIndex: int
  - Speed: float                // from server (via MoveEvent)
  - Target: Vec2                // current move target

Methods:
  - OnClickMove(targetPos: Vec2): void
      → local pathfind (A* + Funnel)
      → start moving along local path immediately
      → send MoveRequest(targetPos) to server
  
  - Tick(deltaTime: float): void
      → advance position along local path (same logic as server)
  
  - OnPositionSync(sync: PositionSync): void
      → correction logic (see below)
```

### Position Correction Strategy

```
OnPositionSync(serverPos, isMoving):
  distance = Position.DistanceTo(serverPos)
  
  if distance < 0.1:            // SNAP_THRESHOLD: negligible, ignore
    return
  elif distance < 2.0:          // SMOOTH_THRESHOLD: smooth correction
    Position = Lerp(Position, serverPos, 0.3)
  else:                          // severe divergence: snap to server
    Position = serverPos
    re-pathfind from serverPos to Target

  if (!isMoving):               // server says stopped
    stop local movement
```

### RemotePlayerController

Other players also run local prediction (same as local player). On receiving MoveEvent, pathfind locally and start moving. On receiving PositionSync, correct drift:

```
Fields:
  - Position: Vec2              // local predicted position
  - Path: Vec2[]                // pathfinding result (computed on MoveEvent)
  - PathIndex: int
  - Speed: float                // from MoveEvent
  - Target: Vec2

Methods:
  - OnMoveEvent(evt: MoveEvent): void
      → Target = evt.target
      → Speed = evt.speed
      → local pathfind (A* + Funnel) from current Position to Target
      → start moving along path
  
  - Tick(deltaTime: float): void
      → advance position along path (same tick logic)
  
  - OnPositionSync(sync: PositionSync): void
      → same correction logic as LocalPlayerController
```

## File Structure

### New Files

```
Server/StateSync.Server/
  Game/
    GameLoop.cs
    PlayerEntity.cs
    MoveController.cs
  Network/
    ClientSession.cs      // new: per-connection state + push

Client/Assets/Scripts/
  Movement/
    LocalPlayerController.cs
    RemotePlayerController.cs

Share/Proto/
  messages.proto          // modified: add MoveRequest, MoveEvent, PositionSync
  message_type.proto      // modified: add MOVE_REQUEST, MOVE_EVENT, POSITION_SYNC
```

### Modified Files

```
Server/StateSync.Server/
  Game/Room.cs                  // add: player entities, input queue, sessions list
  Network/TcpServer.cs          // refactor: ClientSession management, non-request-response flow
  Network/MessageDispatcher.cs  // add: MoveRequest handler (queue into room)
  Program.cs                    // start GameLoop

Client/Assets/Scripts/
  Network/NetworkManager.cs     // register MoveEvent + PositionSync handlers
  World/WorldManager.cs         // integrate movement controllers
```

## Future Extensions (Not In Current Scope)

### Extension 1: Server GM Visual Monitor

Real-time web-based visualization of server game state:
- Display all player authoritative positions on NavMesh
- Show paths, movement vectors, tick timing
- Compare server state vs client-reported state for latency analysis
- Useful for debugging desync between client prediction and server authority

### Extension 2: RTT Calculation and Prediction Compensation

Client-side RTT measurement and adaptive prediction:
- Ping/Pong protocol for RTT measurement
- Prediction advance = speed × RTT/2 (compensate for round-trip)
- Dynamic adjustment of interpolation buffer and correction thresholds based on RTT
- Smoothing of RTT measurements (exponential moving average)

### Not Planned

- AOI (Area of Interest) — not needed at ≤16 players
- Collision detection / skill hit detection
- Anti-cheat beyond basic server authority
