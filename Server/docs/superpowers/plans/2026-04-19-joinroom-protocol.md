# JoinRoom Protocol Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现基于原始 TCP + Protobuf 的 JoinRoom 协议，包含服务端完整逻辑和自动化测试。

**Architecture:** TcpServer 接受客户端连接，每条连接用 PacketReader 读取自定义帧，MessageDispatcher 按消息类型路由到 RoomManager 处理，PacketWriter 组装响应帧发回客户端。客户端→服务端帧头固定 8 字节，服务端→客户端帧头可变（含错误码和参数）。

**Tech Stack:** C# .NET 9, Google.Protobuf 3.34.1, Grpc.Tools 2.80.0（仅用于 proto 代码生成），xUnit 2.6.6

---

## 文件清单

**新建：**
- `StateSnyc/Proto/error_codes.proto`
- `StateSnyc/Proto/messages.proto`
- `StateSnyc/Server/StateSync.Server/Network/MessageType.cs`
- `StateSnyc/Server/StateSync.Server/Network/PacketWriter.cs`
- `StateSnyc/Server/StateSync.Server/Network/PacketReader.cs`
- `StateSnyc/Server/StateSync.Server/Network/MessageDispatcher.cs`
- `StateSnyc/Server/StateSync.Server/Network/TcpServer.cs`
- `StateSnyc/Server/StateSync.Server/Game/Room.cs`
- `StateSnyc/Server/StateSync.Server/Game/RoomManager.cs`
- `StateSnyc/Server/StateSync.Server.Tests/StateSync.Server.Tests.csproj`
- `StateSnyc/Server/StateSync.Server.Tests/Network/PacketWriterTests.cs`
- `StateSnyc/Server/StateSync.Server.Tests/Network/PacketReaderTests.cs`
- `StateSnyc/Server/StateSync.Server.Tests/Game/RoomManagerTests.cs`

**修改：**
- `StateSnyc/Server/StateSync.Server/StateSync.Server.csproj`（更新 Proto 路径）
- `StateSnyc/Server/StateSync.Server/Program.cs`（启动服务器）

---

## Task 1: 创建 Proto 文件

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Proto/error_codes.proto`
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Proto/messages.proto`

- [ ] **Step 1: 创建 Proto 目录并写入 error_codes.proto**

```protobuf
// e:/Learning/MyProject/GitHub/StateSnyc/Proto/error_codes.proto
syntax = "proto3";
option csharp_namespace = "StateSync.Shared";

enum ErrorCode {
    SUCCESS = 0;
    ROOM_NOT_FOUND = 100;
    ROOM_FULL = 101;
    ROOM_ALREADY_JOINED = 102;
    PLAYER_NOT_IN_ROOM = 200;
    GAME_NOT_STARTED = 300;
    INVALID_INPUT = 301;
}
```

- [ ] **Step 2: 写入 messages.proto**

```protobuf
// e:/Learning/MyProject/GitHub/StateSnyc/Proto/messages.proto
syntax = "proto3";
option csharp_namespace = "StateSync.Shared";

message JoinRoom {
    string room_id = 1;
    string player_id = 2;
    repeated string player_ids = 3;
}

message PlayerJoined {
    string player_id = 1;
}
```

- [ ] **Step 3: 更新 .csproj 中的 Proto 路径**

将 `StateSync.Server.csproj` 中的：
```xml
<Protobuf Include="Proto/**/*.proto" GrpcServices="None" />
```
改为：
```xml
<Protobuf Include="../../Proto/**/*.proto" GrpcServices="None" />
```

- [ ] **Step 4: 构建项目，验证 proto 代码生成成功**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server"
dotnet build
```

期望输出：`Build succeeded`，无错误。生成的代码位于 `obj/Debug/net9.0/` 下。

- [ ] **Step 5: 提交**

```bash
git -C "e:/Learning/MyProject/GitHub/StateSnyc" add Proto/ Server/StateSync.Server/StateSync.Server.csproj
git -C "e:/Learning/MyProject/GitHub/StateSnyc" commit -m "feat: add shared proto definitions and update csproj proto path"
```

---

## Task 2: 创建测试项目

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/StateSync.Server.Tests.csproj`

- [ ] **Step 1: 创建测试项目文件**

```xml
<!-- e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/StateSync.Server.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../StateSync.Server/StateSync.Server.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 验证测试项目可以构建**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet build
```

期望输出：`Build succeeded`

---

## Task 3: 定义 MessageType 枚举

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageType.cs`

- [ ] **Step 1: 创建 MessageType.cs**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageType.cs
namespace StateSync.Server.Network;

public enum MessageType
{
    JoinRoom = 1,
    LeaveRoom = 2,
    PlayerJoined = 3,
    PlayerInput = 4,
    WorldState = 5
}
```

- [ ] **Step 2: 构建验证**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server"
dotnet build
```

期望输出：`Build succeeded`

---

## Task 4: TDD PacketWriter

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Network/PacketWriterTests.cs`
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/PacketWriter.cs`

- [ ] **Step 1: 写失败测试**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Network/PacketWriterTests.cs
namespace StateSync.Server.Tests.Network;

using StateSync.Server.Network;
using StateSync.Shared;

public class PacketWriterTests
{
    [Fact]
    public void WriteServerPacket_Success_NoParams_CorrectLayout()
    {
        byte[] data = [10, 20, 30];
        byte[] packet = PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.Success, [], data);

        Assert.Equal(1, BitConverter.ToInt32(packet, 0));   // type = JoinRoom
        Assert.Equal(3, BitConverter.ToInt32(packet, 4));   // data length
        Assert.Equal(0, BitConverter.ToInt32(packet, 8));   // error code = SUCCESS
        Assert.Equal(0, BitConverter.ToInt32(packet, 12));  // param count = 0
        Assert.Equal(data, packet[16..]);
    }

    [Fact]
    public void WriteServerPacket_RoomFull_OneParam_CorrectLayout()
    {
        byte[] packet = PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.RoomFull, [16], []);

        Assert.Equal(101, BitConverter.ToInt32(packet, 8));  // ROOM_FULL = 101
        Assert.Equal(1, BitConverter.ToInt32(packet, 12));   // param count = 1
        Assert.Equal(16, BitConverter.ToInt32(packet, 16));  // param[0] = max players
        Assert.Equal(20, packet.Length);                     // 16-byte header + 1×4-byte param
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test --filter "FullyQualifiedName~PacketWriterTests" -v normal
```

期望：编译错误 `The type or namespace name 'PacketWriter' could not be found`

- [ ] **Step 3: 实现 PacketWriter**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/PacketWriter.cs
namespace StateSync.Server.Network;

using StateSync.Shared;

public static class PacketWriter
{
    public static byte[] WriteServerPacket(MessageType type, ErrorCode errorCode, int[] errorParams, byte[] data)
    {
        int headerSize = 16 + errorParams.Length * 4;
        byte[] packet = new byte[headerSize + data.Length];
        int offset = 0;

        Write(packet, ref offset, (int)type);
        Write(packet, ref offset, data.Length);
        Write(packet, ref offset, (int)errorCode);
        Write(packet, ref offset, errorParams.Length);
        foreach (var p in errorParams)
            Write(packet, ref offset, p);
        data.CopyTo(packet, offset);

        return packet;
    }

    private static void Write(byte[] buf, ref int offset, int value)
    {
        BitConverter.TryWriteBytes(buf.AsSpan(offset), value);
        offset += 4;
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test --filter "FullyQualifiedName~PacketWriterTests" -v normal
```

期望：`Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: 提交**

```bash
git -C "e:/Learning/MyProject/GitHub/StateSnyc" add Server/
git -C "e:/Learning/MyProject/GitHub/StateSnyc" commit -m "feat: add PacketWriter with server-to-client frame format"
```

---

## Task 5: TDD PacketReader

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Network/PacketReaderTests.cs`
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/PacketReader.cs`

- [ ] **Step 1: 写失败测试**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Network/PacketReaderTests.cs
namespace StateSync.Server.Tests.Network;

using StateSync.Server.Network;

public class PacketReaderTests
{
    [Fact]
    public async Task ReadClientPacketAsync_ValidPacket_ReturnsTypeAndData()
    {
        byte[] payload = [1, 2, 3];
        byte[] packet = BuildClientPacket(MessageType.JoinRoom, payload);
        using var stream = new MemoryStream(packet);

        var (type, data) = await PacketReader.ReadClientPacketAsync(stream);

        Assert.Equal(MessageType.JoinRoom, type);
        Assert.Equal(payload, data);
    }

    [Fact]
    public async Task ReadClientPacketAsync_EmptyData_ReturnsEmptyArray()
    {
        byte[] packet = BuildClientPacket(MessageType.JoinRoom, []);
        using var stream = new MemoryStream(packet);

        var (_, data) = await PacketReader.ReadClientPacketAsync(stream);

        Assert.Empty(data);
    }

    [Fact]
    public async Task ReadClientPacketAsync_StreamClosed_ThrowsEndOfStreamException()
    {
        using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PacketReader.ReadClientPacketAsync(stream));
    }

    // 构建客户端格式包（8字节头 + 数据）
    private static byte[] BuildClientPacket(MessageType type, byte[] data)
    {
        byte[] packet = new byte[8 + data.Length];
        BitConverter.TryWriteBytes(packet.AsSpan(0), (int)type);
        BitConverter.TryWriteBytes(packet.AsSpan(4), data.Length);
        data.CopyTo(packet, 8);
        return packet;
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test --filter "FullyQualifiedName~PacketReaderTests" -v normal
```

期望：编译错误 `The type or namespace name 'PacketReader' could not be found`

- [ ] **Step 3: 实现 PacketReader**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/PacketReader.cs
namespace StateSync.Server.Network;

public static class PacketReader
{
    public static async Task<(MessageType Type, byte[] Data)> ReadClientPacketAsync(Stream stream)
    {
        byte[] header = await ReadExactAsync(stream, 8);
        var type = (MessageType)BitConverter.ToInt32(header, 0);
        int length = BitConverter.ToInt32(header, 4);
        byte[] data = length > 0 ? await ReadExactAsync(stream, length) : [];
        return (type, data);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
    {
        byte[] buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(read, count - read));
            if (n == 0) throw new EndOfStreamException("Client disconnected");
            read += n;
        }
        return buf;
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test --filter "FullyQualifiedName~PacketReaderTests" -v normal
```

期望：`Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: 提交**

```bash
git -C "e:/Learning/MyProject/GitHub/StateSnyc" add Server/
git -C "e:/Learning/MyProject/GitHub/StateSnyc" commit -m "feat: add PacketReader with client-to-server frame parsing"
```

---

## Task 6: TDD Room

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Game/Room.cs`

Room 是纯数据结构，在 RoomManagerTests 中一并测试，此处只实现。

- [ ] **Step 1: 实现 Room**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Game/Room.cs
namespace StateSync.Server.Game;

public class Room
{
    public string RoomId { get; }
    public int MaxPlayers { get; }
    private readonly List<string> _players = [];

    public Room(string roomId, int maxPlayers = 16)
    {
        RoomId = roomId;
        MaxPlayers = maxPlayers;
    }

    public bool IsFull => _players.Count >= MaxPlayers;
    public IReadOnlyList<string> Players => _players;

    public void Add(string playerId) => _players.Add(playerId);
}
```

- [ ] **Step 2: 构建验证**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server"
dotnet build
```

期望：`Build succeeded`

---

## Task 7: TDD RoomManager

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Game/RoomManagerTests.cs`
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Game/RoomManager.cs`

- [ ] **Step 1: 写失败测试**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests/Game/RoomManagerTests.cs
namespace StateSync.Server.Tests.Game;

using StateSync.Server.Game;
using StateSync.Shared;

public class RoomManagerTests
{
    private readonly RoomManager _manager = new();

    public RoomManagerTests()
    {
        _manager.CreateRoom("room1", maxPlayers: 2);
    }

    [Fact]
    public void HandleJoinRoom_Success_ReturnsAssignedPlayerIdAndAllPlayers()
    {
        var (error, _, response) = _manager.HandleJoinRoom("room1");

        Assert.Equal(ErrorCode.Success, error);
        Assert.NotNull(response);
        Assert.NotEmpty(response.PlayerId);
        Assert.Single(response.PlayerIds);
    }

    [Fact]
    public void HandleJoinRoom_RoomNotFound_ReturnsErrorNoParams()
    {
        var (error, errorParams, response) = _manager.HandleJoinRoom("missing");

        Assert.Equal(ErrorCode.RoomNotFound, error);
        Assert.Empty(errorParams);
        Assert.Null(response);
    }

    [Fact]
    public void HandleJoinRoom_RoomFull_ReturnsErrorWithMaxPlayersParam()
    {
        _manager.HandleJoinRoom("room1");
        _manager.HandleJoinRoom("room1");

        var (error, errorParams, response) = _manager.HandleJoinRoom("room1");

        Assert.Equal(ErrorCode.RoomFull, error);
        Assert.Equal(2, errorParams[0]);
        Assert.Null(response);
    }

    [Fact]
    public void HandleJoinRoom_SecondPlayer_PlayerIdsContainsBothPlayers()
    {
        _manager.HandleJoinRoom("room1");
        var (_, _, response) = _manager.HandleJoinRoom("room1");

        Assert.Equal(2, response!.PlayerIds.Count);
    }
}
```

- [ ] **Step 2: 运行测试，确认失败**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test --filter "FullyQualifiedName~RoomManagerTests" -v normal
```

期望：编译错误 `The type or namespace name 'RoomManager' could not be found`

- [ ] **Step 3: 实现 RoomManager**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Game/RoomManager.cs
namespace StateSync.Server.Game;

using StateSync.Shared;

public class RoomManager
{
    private readonly Dictionary<string, Room> _rooms = [];

    public void CreateRoom(string roomId, int maxPlayers = 16) =>
        _rooms[roomId] = new Room(roomId, maxPlayers);

    public (ErrorCode Error, int[] ErrorParams, JoinRoom? Response) HandleJoinRoom(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return (ErrorCode.RoomNotFound, [], null);

        if (room.IsFull)
            return (ErrorCode.RoomFull, [room.MaxPlayers], null);

        var playerId = Guid.NewGuid().ToString("N")[..8];
        room.Add(playerId);

        var response = new JoinRoom { RoomId = roomId, PlayerId = playerId };
        response.PlayerIds.AddRange(room.Players);
        return (ErrorCode.Success, [], response);
    }
}
```

- [ ] **Step 4: 运行测试，确认通过**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test --filter "FullyQualifiedName~RoomManagerTests" -v normal
```

期望：`Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: 提交**

```bash
git -C "e:/Learning/MyProject/GitHub/StateSnyc" add Server/
git -C "e:/Learning/MyProject/GitHub/StateSnyc" commit -m "feat: add Room and RoomManager with JoinRoom logic"
```

---

## Task 8: 实现 MessageDispatcher

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageDispatcher.cs`

- [ ] **Step 1: 实现 MessageDispatcher**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/MessageDispatcher.cs
namespace StateSync.Server.Network;

using StateSync.Server.Game;
using StateSync.Shared;

public class MessageDispatcher
{
    private readonly RoomManager _roomManager;

    public MessageDispatcher(RoomManager roomManager) => _roomManager = roomManager;

    public byte[] Dispatch(MessageType type, byte[] data) => type switch
    {
        MessageType.JoinRoom => HandleJoinRoom(data),
        _ => PacketWriter.WriteServerPacket(type, ErrorCode.InvalidInput, [], [])
    };

    private byte[] HandleJoinRoom(byte[] data)
    {
        var request = JoinRoom.Parser.ParseFrom(data);
        var (error, errorParams, response) = _roomManager.HandleJoinRoom(request.RoomId);
        byte[] responseData = response?.ToByteArray() ?? [];
        return PacketWriter.WriteServerPacket(MessageType.JoinRoom, error, errorParams, responseData);
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server"
dotnet build
```

期望：`Build succeeded`

---

## Task 9: 实现 TcpServer

**Files:**
- Create: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/TcpServer.cs`

- [ ] **Step 1: 实现 TcpServer**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Network/TcpServer.cs
namespace StateSync.Server.Network;

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
        while (!ct.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(ct);
            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var (type, data) = await PacketReader.ReadClientPacketAsync(stream);
                    byte[] response = _dispatcher.Dispatch(type, data);
                    await stream.WriteAsync(response, ct);
                }
            }
            catch (EndOfStreamException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"Client error: {ex.Message}"); }
        }
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server"
dotnet build
```

期望：`Build succeeded`

---

## Task 10: 连接 Program.cs 并全量测试

**Files:**
- Modify: `e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Program.cs`

- [ ] **Step 1: 更新 Program.cs**

```csharp
// e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server/Program.cs
using StateSync.Server.Game;
using StateSync.Server.Network;

var roomManager = new RoomManager();
roomManager.CreateRoom("room1");

var dispatcher = new MessageDispatcher(roomManager);
var server = new TcpServer(7777, dispatcher);

await server.StartAsync();
```

- [ ] **Step 2: 运行全量测试**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server.Tests"
dotnet test -v normal
```

期望：`Passed! - Failed: 0, Passed: 9`（PacketWriter×2 + PacketReader×3 + RoomManager×4）

- [ ] **Step 3: 启动服务器验证可运行**

```bash
cd "e:/Learning/MyProject/GitHub/StateSnyc/Server/StateSync.Server"
dotnet run
```

期望输出：`Listening on port 7777`（Ctrl+C 退出）

- [ ] **Step 4: 最终提交**

```bash
git -C "e:/Learning/MyProject/GitHub/StateSnyc" add Server/
git -C "e:/Learning/MyProject/GitHub/StateSnyc" commit -m "feat: implement JoinRoom protocol with TCP server, dispatcher and room manager"
```
