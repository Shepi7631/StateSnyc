# JoinRoom 协议设计文档

## 项目背景

StateSync 是一个 Unity 游戏客户端 + C# .NET 9 服务端的多人游戏状态同步系统。
传输层使用原始 TCP，序列化使用 Protobuf，服务端为权威服务器模式，每房间支持 5-16 人，状态更新频率 20-30hz。

## 目录结构

```
StateSnyc/
├── Proto/                          # 共享 Proto 文件，双端共用
│   ├── messages.proto              # 消息结构定义
│   └── error_codes.proto          # 错误码枚举
│
├── Server/
│   └── StateSync.Server/
│       ├── Network/
│       │   ├── TcpServer.cs        # TCP 监听与连接管理
│       │   ├── PacketReader.cs     # 拆帧（读取完整消息）
│       │   ├── PacketWriter.cs     # 组帧（写入消息）
│       │   └── MessageDispatcher.cs # 按消息类型分发处理
│       └── Game/
│           ├── Room.cs             # 房间数据结构
│           └── RoomManager.cs      # 加入/离开房间逻辑
│
└── Client/
    └── Assets/
        ├── Proto/                  # protoc 生成的 .cs 文件
        └── Scripts/Network/
            ├── TcpGameClient.cs    # TCP 连接管理
            ├── PacketReader.cs     # 拆帧
            └── PacketWriter.cs     # 组帧
```

## 消息帧协议

### 客户端 → 服务端（8字节固定头）

```
┌──────────┬──────────┬─────────────────────┐
│  4字节   │  4字节   │     N字节            │
│ 消息类型  │ 数据长度  │  Protobuf 序列化数据  │
└──────────┴──────────┴─────────────────────┘
```

### 服务端 → 客户端（可变长头）

```
┌──────────┬──────────┬──────────┬──────────┬──────────────┬─────────────────────┐
│  4字节   │  4字节   │  4字节   │  4字节   │   M×4字节    │     N字节            │
│ 消息类型  │ 数据长度  │  错误码  │ 参数个数M │  M个错误参数  │  Protobuf 序列化数据  │
└──────────┴──────────┴──────────┴──────────┴──────────────┴─────────────────────┘
```

- 错误码 = 0 表示成功，参数个数 = 0
- 出错时 Protobuf 数据长度可为 0
- 错误参数含义由错误码定义，双端按顺序读取

## 消息类型定义

| 值 | 名称 | C→S 含义 | S→C 含义 |
|----|------|----------|----------|
| 1 | JoinRoom | 请求加入房间 | 加入结果（含玩家列表）|
| 2 | LeaveRoom | 请求离开房间 | 广播某玩家已离开 |
| 3 | PlayerJoined | - | 广播新玩家加入 |
| 4 | PlayerInput | 发送玩家输入 | - |
| 5 | WorldState | - | 广播权威世界状态 |

## 错误码定义

```protobuf
enum ErrorCode {
    SUCCESS = 0;

    // 房间错误 (1xx)
    ROOM_NOT_FOUND = 100;       // 参数：无
    ROOM_FULL = 101;            // 参数：[最大人数]
    ROOM_ALREADY_JOINED = 102;  // 参数：无

    // 玩家错误 (2xx)
    PLAYER_NOT_IN_ROOM = 200;   // 参数：无

    // 游戏错误 (3xx)
    GAME_NOT_STARTED = 300;     // 参数：无
    INVALID_INPUT = 301;        // 参数：[非法字段ID]
}
```

## Proto 消息定义

```protobuf
syntax = "proto3";
option csharp_namespace = "StateSync.Shared";

message JoinRoom {
    string room_id = 1;
    string player_id = 2;           // S→C：服务器分配的ID
    repeated string player_ids = 3; // S→C：当前房间所有玩家ID
}
```

## JoinRoom 流程

```
Client 建立 TCP 连接
    │
    ├─发送─▶ [type=1][length=N][JoinRoom{room_id="room1"}]
    │
Server PacketReader 拆帧
    │
    └─▶ MessageDispatcher 路由到 RoomManager.HandleJoinRoom()
            │
            ├─ 房间不存在 → 回包 [type=1][0][100][0][]
            ├─ 房间已满   → 回包 [type=1][0][101][1][16]
            └─ 成功       → 分配 player_id
                           → 回包 [type=1][N][0][0][JoinRoom{room_id, player_id, player_ids}]
                           → 向房间其他玩家广播 [type=3][N][0][0][PlayerJoined{player_id}]

Client PacketReader 拆帧
    │
    └─▶ 读取错误码
            ├─ 非0 → 按错误码+参数显示错误信息
            └─ 0   → 解析 JoinRoom，初始化本地玩家列表
```

## 粘包处理策略

1. 先读 8 字节（客户端包头）或 16+ 字节（服务端包头）
2. 从头部取出 `数据长度 N`
3. 继续读取 N 字节 Protobuf 数据
4. 不足时持续等待，直到缓冲区积累足够字节

## 服务端 .csproj Proto 引用

```xml
<Protobuf Include="../../Proto/**/*.proto" GrpcServices="None" />
```

Unity 端手动运行 `protoc` 从 `../../Proto/` 生成 `.cs` 到 `Assets/Proto/`。
