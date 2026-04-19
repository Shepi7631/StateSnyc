# CreateRoom 协议设计文档

## 项目背景

StateSync 多人游戏状态同步服务器。现有 JoinRoom 协议已实现，本文档描述新增的 CreateRoom 协议，允许客户端主动请求服务器创建房间。

## 需求

- 客户端发送创建房间请求，只需提供最大人数
- 服务器自动生成唯一的6位数字房间ID（如 "384920"）
- 房间ID冲突时重新生成，直到唯一
- 创建成功后返回房间ID，客户端再通过 JoinRoom 加入
- CreateRoom 与 JoinRoom 完全独立，职责分离

## 消息帧协议

沿用现有协议，无变更：
- C→S：`[4字节 MessageType][4字节 数据长度][N字节 Protobuf]`
- S→C：`[4字节 MessageType][4字节 数据长度][4字节 ErrorCode][4字节 参数个数M][M×4字节 参数][N字节 Protobuf]`

## Proto 定义

### messages.proto 新增

```protobuf
message CreateRoom {
    int32 max_players = 1;  // C→S: 最大人数（1-16）
    string room_id = 2;     // S→C: 服务器生成的6位数字房间ID
}
```

### error_codes.proto 新增

```protobuf
INVALID_MAX_PLAYERS = 103;  // max_players ≤ 0 或 > 16
```

## 消息类型

```csharp
// MessageType.cs 新增
CreateRoom = 6,
```

## 交互流程

```
客户端发送：
[type=6][length=N][CreateRoom{ max_players=8 }]

服务器处理：
  1. 反序列化 CreateRoom 请求
  2. 校验 max_players（范围 1-16）
     - 不合法 → 返回 INVALID_MAX_PLAYERS 错误
  3. 生成6位随机数字 room_id（000000-999999）
  4. 检查 room_id 是否已存在，冲突则重新生成（最多重试10次）
  5. 调用 RoomManager.HandleCreateRoom(max_players)
  6. 返回成功响应

服务器返回（成功）：
[type=6][length=N][0][0][CreateRoom{ room_id="384920" }]

服务器返回（失败，max_players 非法）：
[type=6][0][103][0][]
```

## 错误码说明

| ErrorCode | 值 | 参数 | 说明 |
|-----------|---|------|------|
| SUCCESS | 0 | 无 | 创建成功，room_id 在响应消息中 |
| INVALID_MAX_PLAYERS | 103 | 无 | max_players 不在 1-16 范围内 |

## 涉及修改的文件

| 文件 | 变更类型 | 说明 |
|-----|---------|------|
| `Proto/messages.proto` | 新增 | CreateRoom 消息定义 |
| `Proto/error_codes.proto` | 新增 | INVALID_MAX_PLAYERS = 103 |
| `Server/Network/MessageType.cs` | 新增 | CreateRoom = 6 |
| `Server/Network/MessageDispatcher.cs` | 新增 | HandleCreateRoom 路由和处理 |
| `Server/Game/RoomManager.cs` | 新增 | HandleCreateRoom 业务逻辑 |

**不修改：**
- `PacketReader.cs` — 帧读取逻辑无需变更
- `PacketWriter.cs` — 帧组装逻辑无需变更
- `TcpServer.cs` — TCP 层无需变更
- `Room.cs` — 数据结构无需变更

## 测试用例

| 测试 | 输入 | 期望结果 |
|-----|------|---------|
| 正常创建 | max_players=8 | SUCCESS，room_id 为6位数字字符串 |
| 最小值边界 | max_players=1 | SUCCESS |
| 最大值边界 | max_players=16 | SUCCESS |
| 超出上限 | max_players=17 | INVALID_MAX_PLAYERS |
| 零值 | max_players=0 | INVALID_MAX_PLAYERS |
| 负值 | max_players=-1 | INVALID_MAX_PLAYERS |
