# CreateRoom Protocol Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 CreateRoom 协议，允许客户端发送最大人数，服务器自动生成6位数字房间ID并创建房间。

**Architecture:** 沿用现有帧协议和分层设计。新增 MessageType.CreateRoom、proto 消息和错误码，在 RoomManager 添加 HandleCreateRoom 业务逻辑，在 MessageDispatcher 添加路由分支，完全不影响现有 JoinRoom 逻辑。

**Tech Stack:** C# .NET 9, Google.Protobuf 3.34.1, Grpc.Tools 2.80.0, xUnit 2.6.6

---

## 文件清单

**修改：**
- `e:/Learning/MyProject/GitHub/StateSnyc/Proto/error_codes.proto` — 新增 INVALID_MAX_PLAYERS = 103
- `e:/Learning/MyProject/GitHub/StateSnyc/Proto/messages.proto` — 新增 CreateRoom 消息
- `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageType.cs` — 新增 CreateRoom = 6
- `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageDispatcher.cs` — 新增 HandleCreateRoom 路由
- `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Game/RoomManager.cs` — 新增 HandleCreateRoom 方法

**修改（测试）：**
- `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Game/RoomManagerTests.cs` — 新增 CreateRoom 测试用例

---

## Task 1: 更新 Proto 文件

**Files:**
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Proto/error_codes.proto`
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Proto/messages.proto`

- [ ] **Step 1: 新增错误码 INVALID_MAX_PLAYERS**

将 `error_codes.proto` 改为：

```protobuf
syntax = "proto3";
option csharp_namespace = "StateSync.Shared";

enum ErrorCode {
    SUCCESS = 0;
    ROOM_NOT_FOUND = 100;
    ROOM_FULL = 101;
    ROOM_ALREADY_JOINED = 102;
    INVALID_MAX_PLAYERS = 103;
    PLAYER_NOT_IN_ROOM = 200;
    GAME_NOT_STARTED = 300;
    INVALID_INPUT = 301;
}
```

- [ ] **Step 2: 新增 CreateRoom 消息**

将 `messages.proto` 改为：

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
    int32 max_players = 1;  // C→S: 最大人数（1-16）
    string room_id = 2;     // S→C: 服务器生成的6位数字房间ID
}
```

- [ ] **Step 3: 构建验证 proto 编译成功**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server" && dotnet build
```

期望：`Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Task 2: 新增 MessageType 和 TDD RoomManager.HandleCreateRoom

**Files:**
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageType.cs`
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Game/RoomManagerTests.cs`
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Game/RoomManager.cs`

- [ ] **Step 1: 新增 MessageType.CreateRoom**

将 `MessageType.cs` 改为：

```csharp
namespace StateSync.Server.Network;

public enum MessageType
{
    JoinRoom = 1,
    LeaveRoom = 2,
    PlayerJoined = 3,
    PlayerInput = 4,
    WorldState = 5,
    CreateRoom = 6
}
```

- [ ] **Step 2: 写失败测试**

在 `RoomManagerTests.cs` 末尾、类的最后一个 `}` 前添加以下测试方法：

```csharp
    [Fact]
    public void HandleCreateRoom_ValidMaxPlayers_ReturnsSuccessWithSixDigitRoomId()
    {
        var (error, _, response) = _manager.HandleCreateRoom(8);

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
        Assert.Equal(6, response.RoomId.Length);
        Assert.True(response.RoomId.All(char.IsDigit));
    }

    [Fact]
    public void HandleCreateRoom_MaxPlayersZero_ReturnsInvalidMaxPlayers()
    {
        var (error, errorParams, response) = _manager.HandleCreateRoom(0);

        Assert.Equal(ErrorCode.InvalidMaxPlayers, error);
        Assert.Empty(errorParams);
        Assert.Null(response);
    }

    [Fact]
    public void HandleCreateRoom_MaxPlayersNegative_ReturnsInvalidMaxPlayers()
    {
        var (error, _, response) = _manager.HandleCreateRoom(-1);

        Assert.Equal(ErrorCode.InvalidMaxPlayers, error);
        Assert.Null(response);
    }

    [Fact]
    public void HandleCreateRoom_MaxPlayersExceedsLimit_ReturnsInvalidMaxPlayers()
    {
        var (error, _, response) = _manager.HandleCreateRoom(17);

        Assert.Equal(ErrorCode.InvalidMaxPlayers, error);
        Assert.Null(response);
    }

    [Fact]
    public void HandleCreateRoom_BoundaryMin_ReturnsSuccess()
    {
        var (error, _, response) = _manager.HandleCreateRoom(1);

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
    }

    [Fact]
    public void HandleCreateRoom_BoundaryMax_ReturnsSuccess()
    {
        var (error, _, response) = _manager.HandleCreateRoom(16);

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
    }

    [Fact]
    public void HandleCreateRoom_CalledTwice_ReturnsDifferentRoomIds()
    {
        var (_, _, response1) = _manager.HandleCreateRoom(8);
        var (_, _, response2) = _manager.HandleCreateRoom(8);

        Assert.NotEqual(response1!.RoomId, response2!.RoomId);
    }
```

- [ ] **Step 3: 运行测试，确认失败**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests" && dotnet test --filter "FullyQualifiedName~HandleCreateRoom" 2>&1 | head -20
```

期望：编译错误 `'RoomManager' does not contain a definition for 'HandleCreateRoom'`

- [ ] **Step 4: 实现 HandleCreateRoom**

将 `RoomManager.cs` 改为：

```csharp
namespace StateSync.Server.Game;

using System.Collections.Concurrent;
using StateSync.Shared;

public class RoomManager
{
    private const int MaxAllowedPlayers = 16;
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly Random _random = new();

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
        for (int i = 0; i < 10; i++)
        {
            var id = _random.Next(0, 1_000_000).ToString("D6");
            if (!_rooms.ContainsKey(id)) return id;
        }
        return Guid.NewGuid().ToString("N")[..6];
    }
}
```

- [ ] **Step 5: 运行测试，确认通过**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests" && dotnet test --filter "FullyQualifiedName~HandleCreateRoom" -v normal
```

期望：`Passed! - Failed: 0, Passed: 7`

---

## Task 3: 新增 MessageDispatcher.HandleCreateRoom

**Files:**
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageDispatcher.cs`

- [ ] **Step 1: 实现 HandleCreateRoom 并注册路由**

将 `MessageDispatcher.cs` 改为：

```csharp
namespace StateSync.Server.Network;

using Google.Protobuf;
using StateSync.Server.Game;
using StateSync.Shared;

public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    public MessageDispatcher(RoomManager roomManager) => _roomManager = roomManager;

    public byte[] Dispatch(MessageType type, byte[] data) => type switch
    {
        MessageType.JoinRoom   => HandleJoinRoom(data),
        MessageType.CreateRoom => HandleCreateRoom(data),
        _ => PacketWriter.WriteServerPacket(type, ErrorCode.InvalidInput, [], [])
    };

    private byte[] HandleJoinRoom(byte[] data)
    {
        JoinRoom request;
        try { request = JoinRoom.Parser.ParseFrom(data); }
        catch (InvalidProtocolBufferException)
        {
            return PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleJoinRoom(request.RoomId);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.JoinRoom, error, errorParams, responseData);
    }

    private byte[] HandleCreateRoom(byte[] data)
    {
        CreateRoom request;
        try { request = CreateRoom.Parser.ParseFrom(data); }
        catch (InvalidProtocolBufferException)
        {
            return PacketWriter.WriteServerPacket(MessageType.CreateRoom, ErrorCode.InvalidInput, [], []);
        }
        var (error, errorParams, response) = _roomManager.HandleCreateRoom(request.MaxPlayers);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.CreateRoom, error, errorParams, responseData);
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server" && dotnet build
```

期望：`Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Task 4: 全量测试验证

- [ ] **Step 1: 运行全量测试**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests" && dotnet test -v normal
```

期望：`Passed! - Failed: 0, Passed: 16`（原有 9 个 + 新增 7 个）

- [ ] **Step 2: 启动服务器确认可运行**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server" && timeout 3 dotnet run || true
```

期望输出包含：`Listening on port 7777`
