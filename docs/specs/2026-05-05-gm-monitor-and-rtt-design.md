# GM Visual Monitor & RTT Prediction Compensation Design

## Overview

Two independent extensions to the StateSync movement synchronization system:

1. **GM Visual Monitor**: Localhost HTTP server providing a 2D Canvas web page that visualizes server-authoritative game state in real time (via polling). For developer debugging of desync issues.
2. **RTT Calculation & Prediction Compensation**: Server-initiated Ping/Pong protocol for bilateral RTT measurement. Client dynamically adjusts correction thresholds based on measured latency.

## Constraints

- GM Monitor is a dev-only localhost tool (not production-facing)
- HTTP polling at ~200ms interval (not WebSocket)
- RTT measurement is server-authoritative; server shares its measurement with client
- EMA smoothing on RTT samples (alpha = 0.2)
- Both features must not impact game server performance noticeably

---

## Feature 1: GM Visual Monitor

### Architecture

Embed an ASP.NET Core Minimal APIs HTTP server in the existing game server process, running on a separate port (8080). The monitor reads Room/PlayerEntity state directly from memory (read-only) and serves:

1. A JSON REST API for room state snapshots
2. A static HTML page with Canvas-based 2D visualization

```
Browser (Canvas page, port 8080)
    | fetch /api/rooms/{roomId}/state (every 200ms)
    v
MonitorServer (ASP.NET Minimal APIs)
    | direct memory read (thread-safe via existing locks)
    v
Room -> PlayerEntity[] -> position, path, speed, isMoving, rtt
```

### API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | Static HTML/JS visualization page |
| `/api/rooms` | GET | Room list: `[{roomId, playerCount}]` |
| `/api/rooms/{roomId}/state` | GET | Full room snapshot |
| `/api/rooms/{roomId}/navmesh` | GET | NavMesh geometry for map rendering |

### Room State Response Schema

```json
{
  "roomId": "room1",
  "tick": 42,
  "players": [
    {
      "playerId": "abc123",
      "position": { "x": 5.2, "z": 3.1 },
      "target": { "x": 9.0, "z": 9.0 },
      "path": [{"x":5.2,"z":3.1}, {"x":7.0,"z":5.0}, {"x":9.0,"z":9.0}],
      "speed": 5.0,
      "isMoving": true,
      "rttMs": 45
    }
  ]
}
```

### NavMesh Response Schema

```json
{
  "vertices": [{"x":0,"z":0}, {"x":100,"z":0}, {"x":100,"z":100}, {"x":0,"z":100}],
  "triangles": [[0,1,2], [0,2,3]]
}
```

### Frontend Canvas Visualization

The static HTML page renders:
- NavMesh triangles as gray wireframe
- Each player as a colored circle with ID label
- Current path as a dashed line from player to target
- Target position as a small cross marker
- Info panel showing: room tick, player count, per-player RTT and movement state

Auto-refresh via `setInterval(fetch, 200)`.

### Server Implementation: MonitorServer

```
MonitorServer:
  Fields:
    - _app: WebApplication
    - _roomManager: RoomManager

  Constructor(RoomManager, port=8080):
    Build WebApplication with Minimal APIs routes

  Methods:
    - StartAsync(CancellationToken): Task
        Starts the HTTP server

  Routes:
    - GET / -> serve embedded HTML page
    - GET /api/rooms -> list rooms from RoomManager
    - GET /api/rooms/{roomId}/state -> snapshot from Room
    - GET /api/rooms/{roomId}/navmesh -> mesh geometry
```

### PlayerEntity Changes

Expose path data for monitor consumption:

```
New public properties on PlayerEntity:
  - Path: IReadOnlyList<Vec2>     (current waypoint path)
  - PathIndex: int                 (current segment index)
  - Rtt: int                       (RTT in ms, set by RoomLoop from session)
```

### Startup Integration

```csharp
// Program.cs
await Task.WhenAll(
    roomLoop.RunAsync(cts.Token),
    server.StartAsync(cts.Token),
    monitorServer.StartAsync(cts.Token)  // new
);
```

---

## Feature 2: RTT Calculation & Prediction Compensation

### Protocol

New protobuf messages:

```protobuf
message Ping {
    uint64 server_timestamp = 1;  // ms timestamp when server sent this
    uint32 sequence = 2;          // monotonic sequence number
    uint32 your_rtt_ms = 3;      // server's last measured RTT for this client
}

message Pong {
    uint64 server_timestamp = 1;  // echoed from Ping
    uint32 sequence = 2;          // echoed from Ping
}
```

New MessageType entries:

```protobuf
PING = 10;
PONG = 11;
```

### Server-Side RTT Measurement

**Ping frequency**: Every 10 ticks (= 1 second at 10 Hz tick rate).

**RoomLoop integration**:
- On tick where `CurrentTick % 10 == 0`:
  - For each connected session, send `Ping(serverTimestamp=now_ms, sequence++, your_rtt_ms=session.SmoothedRtt)`
  - Record send timestamp per-session

**Pong handling** (in MessageDispatcher):
- On receiving Pong from a session:
  - Compute `rttSample = now_ms - pong.ServerTimestamp`
  - Update `session.SmoothedRtt` via EMA: `smoothed = 0.2 * sample + 0.8 * smoothed`
  - No response sent back

**Data storage**:
```
ClientSession:
  + SmoothedRtt: int              (smoothed RTT in ms, default 0)
  + LastPingSequence: uint32      (for matching, though not strictly needed with timestamps)
```

**RTT propagation to PlayerEntity**:
- In `RoomLoop.ProcessInputs()` or a dedicated sync step, copy each session's SmoothedRtt to the corresponding PlayerEntity.Rtt
- This makes RTT visible to the GM Monitor API

### Client-Side RTT Reception

**Receiving Ping**:
- Network layer receives Ping, immediately sends Pong (echo timestamp + sequence)
- Reads `your_rtt_ms` field and stores as local RTT value
- No game logic involvement — handled at network layer

**Dynamic correction parameter adjustment**:

Based on `rttMs` (the RTT value received from server):

| Parameter | Base Value | Adaptive Formula | Rationale |
|-----------|-----------|------------------|-----------|
| SnapThreshold | 0.1 | Unchanged (always 0.1) | Negligible differences are always ignored |
| SmoothThreshold | 2.0 | `2.0 + speed * rttMs / 1000.0` | Higher RTT = larger acceptable drift before correction |
| CorrectionRate | 0.3 | `clamp(0.1, 0.5, 0.3 - rttMs / 2000.0)` | Higher RTT = gentler correction to avoid jitter |

**Rationale**: When RTT is high, PositionSync data is already stale by RTT/2 ms. Aggressive correction toward stale positions causes rubber-banding. Reducing CorrectionRate and widening SmoothThreshold lets the client trust its own prediction more, resulting in smoother movement at the cost of slightly more drift tolerance.

### Sequence Diagram

```
Server                                     Client
  |                                          |
  |--- Ping(ts=1000, seq=1, rtt=0) -------->|
  |                                          | store rtt=0
  |<-- Pong(ts=1000, seq=1) ----------------|
  |                                          |
  | rttSample = now(1045) - 1000 = 45ms     |
  | smoothedRtt = 0.2*45 + 0.8*0 = 9ms     |
  |                                          |
  |--- Ping(ts=2000, seq=2, rtt=9) -------->|
  |                                          | store rtt=9, adjust thresholds
  |<-- Pong(ts=2000, seq=2) ----------------|
  |                                          |
  | rttSample = now(2048) - 2000 = 48ms     |
  | smoothedRtt = 0.2*48 + 0.8*9 = 16.8ms  |
  |                                          |
  | ... converges toward true RTT ...        |
```

### Edge Cases

- **First Ping**: `your_rtt_ms = 0` (no measurement yet). Client uses base values.
- **Client disconnect during Ping**: Pong never arrives. SmoothedRtt decays naturally on reconnection (fresh session starts at 0).
- **Clock drift**: Not an issue — RTT is measured purely from server's own clock (send vs receive of same message pair).
- **Very high RTT (>500ms)**: CorrectionRate floors at 0.1 (clamped). SmoothThreshold grows but is bounded by practical speeds (speed=5, rtt=500 -> threshold = 2.0 + 5*0.5 = 4.5, reasonable).

---

## File Structure

### New Files

```
Server/StateSync.Server/
  Monitor/
    MonitorServer.cs              // HTTP server setup and routes
    MonitorPage.cs                // Embedded HTML/JS string for Canvas page
  
Client/Assets/Scripts/
  Network/
    RttTracker.cs                 // Client-side RTT storage and threshold computation
```

### Modified Files

```
Share/Proto/
  messages.proto                  // Add Ping, Pong messages
  message_type.proto              // Add PING = 10, PONG = 11

Server/StateSync.Server/
  Game/PlayerEntity.cs            // Expose Path, Rtt properties
  Game/RoomLoop.cs                // Add periodic Ping broadcast, RTT sync to entities
  Game/Room.cs                    // Add method to get session-to-entity RTT mapping
  Network/MessageDispatcher.cs    // Handle Pong message
  Network/ClientSession.cs        // Add SmoothedRtt field
  Program.cs                      // Start MonitorServer
  StateSync.Server.csproj         // Add ASP.NET Core framework reference

Client/Assets/Scripts/
  Movement/LocalPlayerController.cs   // Use RttTracker for dynamic thresholds
  Movement/RemotePlayerController.cs  // Use RttTracker for dynamic thresholds
  Network/NetworkManager.cs           // Handle Ping → send Pong, update RttTracker
```

---

## Dependencies Between Features

RTT feeds into GM Monitor (RTT values displayed per-player), but they can be implemented independently:
- Implement RTT first (proto changes + server/client Ping/Pong logic)
- Then GM Monitor (reads RTT from PlayerEntity.Rtt which is already populated)

Or implement GM Monitor first with RTT showing as 0, then add RTT measurement.

## Non-Goals

- Authentication/authorization on the Monitor endpoint (localhost only)
- Historical replay / recording
- Production deployment of Monitor
- Server-side lag compensation (using RTT for hit detection etc.)
