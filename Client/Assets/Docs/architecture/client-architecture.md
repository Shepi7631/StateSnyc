# StateSync 客户端架构文档

## 一、项目概览

StateSync 是一个基于 TCP + Protobuf 的多人状态同步项目。服务端为 .NET 9 控制台应用，客户端为 Unity 项目。

```
StateSnyc/
├── Proto/          # 共享 Protobuf 定义
├── Server/         # .NET 9 服务端
└── Client/         # Unity 客户端
```

---

## 二、网络协议

### 2.1 传输层

- 协议：TCP
- 默认端口：7777
- 序列化：Google Protobuf
- 字节序：Little-Endian

### 2.2 数据包格式

**客户端 → 服务端（8 字节头 + 载荷）：**

```
┌──────────────┬──────────────┬─────────────────┐
│ MessageType  │ PayloadLen   │ Payload          │
│ int32 (4B)   │ int32 (4B)   │ byte[] (变长)    │
└──────────────┴──────────────┴─────────────────┘
```

**服务端 → 客户端（16+ 字节头 + 载荷）：**

```
┌──────────────┬──────────────┬──────────────┬────────────────┬─────────────────┬──────────────┐
│ MessageType  │ DataLen      │ ErrorCode    │ ErrorParamCnt  │ ErrorParams     │ Data         │
│ int32 (4B)   │ int32 (4B)   │ int32 (4B)   │ int32 (4B)     │ int32[] (N×4B)  │ byte[] (变长)│
└──────────────┴──────────────┴──────────────┴────────────────┴─────────────────┴──────────────┘
```

- 最大载荷大小：1MB
- ErrorParams 长度由 ErrorParamCnt 决定，每个参数 4 字节

### 2.3 消息类型

| 枚举值 | 名称 | 方向 | 说明 |
|--------|------|------|------|
| 0 | Unspecified | - | 未指定 |
| 1 | JoinRoom | C→S | 加入房间 |
| 2 | LeaveRoom | C→S | 离开房间 |
| 3 | PlayerJoined | S→C | 玩家加入通知 |
| 4 | PlayerInput | C→S | 玩家输入 |
| 5 | WorldState | S→C | 世界状态同步 |
| 6 | CreateRoom | C→S | 创建房间 |

### 2.4 错误码

| 枚举值 | 名称 | 说明 |
|--------|------|------|
| 0 | Success | 成功 |
| 100 | RoomNotFound | 房间不存在 |
| 101 | RoomFull | 房间已满 |
| 102 | RoomAlreadyJoined | 已在房间中 |
| 103 | InvalidMaxPlayers | 无效的最大玩家数 |
| 200 | PlayerNotInRoom | 玩家不在房间内 |
| 300 | GameNotStarted | 游戏未开始 |
| 301 | InvalidInput | 无效输入 |

### 2.5 Protobuf 消息定义

```protobuf
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
```

---

## 三、客户端网络框架

### 3.1 文件结构

```
Assets/Scripts/
├── _gen/                           # protobuf 自动生成（勿手动编辑）
│   ├── MessageType.cs              # 消息类型枚举
│   ├── ErrorCodes.cs               # 错误码枚举
│   └── Messages.cs                 # Protobuf 消息类
├── Network/                        # 网络框架（手写）
│   ├── ServerPacket.cs             # 服务端响应包结构体
│   ├── PacketWriter.cs             # 客户端→服务端 编码
│   ├── PacketReader.cs             # 服务端→客户端 解码
│   ├── MessageDispatcher.cs        # 消息分发器
│   ├── NetworkClient.cs            # TCP 连接与收发
│   └── NetworkManager.cs           # 网络层入口（普通 C# 类）
└── UI/
    ├── UIManager.cs                # 协调 NetworkManager + RoomUIController
    ├── RoomUIBuilder.cs            # 运行时以纯代码构建 Canvas 层级
    └── RoomUIController.cs         # 面板切换与网络响应处理
```

### 3.2 架构分层

```
┌──────────────────────────────────────────────────┐
│           MonoBehaviour（场景入口）                │
│   Update() → UIManager.Tick() / NetworkManager.Tick()│
└────────────────────────┬─────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────┐
│                   UIManager                       │
│   协调 NetworkManager + RoomUIController 生命周期  │
└──────────┬─────────────────────────┬─────────────┘
           │                         │
           ▼                         ▼
┌──────────────────────┐  ┌────────────────────────┐
│   NetworkManager     │  │   RoomUIController     │
│   普通 C# 类          │  │   普通 C# 类            │
│   Initialize/Tick    │  │   Initialize/Tick      │
│   /Dispose           │  │   /Dispose             │
└───────┬──────────────┘  └────────────────────────┘
        │
        ▼
┌─────────────────┐  ┌───────────────────────┐
│ MessageDispatcher│  │    NetworkClient       │
│ 类型注册表       │  │ TCP 连接 / 后台线程     │
│ 反序列化 + 分发  │  │ ConcurrentQueue 缓冲   │
└─────────────────┘  └───────┬───────────────┘
                             │
                    ┌────────┴────────┐
                    ▼                 ▼
             PacketWriter       PacketReader
            (编码发送包)        (解码接收包)
```

### 3.3 各类职责

#### ServerPacket

服务端响应的解析结果，纯数据结构体。

```csharp
public struct ServerPacket
{
    public MessageType Type;       // 消息类型
    public ErrorCode Error;        // 错误码
    public int[] ErrorParams;      // 错误参数（如房间容量上限）
    public byte[] Data;            // Protobuf 载荷
}
```

#### PacketWriter

静态工具类。将 `MessageType` + protobuf 载荷编码为客户端发送包（8 字节头 + 载荷）。
与服务端 `PacketReader` 镜像对应。

#### PacketReader

静态工具类。从 `NetworkStream` 读取并解析服务端响应包为 `ServerPacket`。
与服务端 `PacketWriter` 镜像对应。

内部使用 `ReadExact` 方法循环读取，确保在 TCP 分片场景下也能读取完整数据。

#### MessageDispatcher

消息路由中心。维护 `Dictionary<MessageType, Action<ServerPacket>>` 映射表。

- `Register<T>(type, handler)` — 注册处理器，`T` 约束为 `IMessage<T>, new()`，内部闭包持有对应的 `MessageParser<T>` 完成自动反序列化
- `Dispatch(packet)` — 根据 `MessageType` 查表并调用处理器，未注册的类型打印警告
- `Unregister(type)` / `Clear()` — 移除处理器

#### NetworkClient

TCP 连接管理与收发引擎。核心设计：

**线程模型：**

| 线程 | 职责 | 同步机制 |
|------|------|----------|
| 主线程 | `Connect()` / `Send()` / `TryDequeue()` | `lock (_SendLock)` 保护写入 |
| 后台线程 | `ReceiveLoop()` 阻塞读包 → 入队 | `ConcurrentQueue` 无锁队列 |

**后台线程创建方式：**

```csharp
_ReceiveThread = new Thread(ReceiveLoop);
_ReceiveThread.IsBackground = true;   // 进程退出时自动终止，防止卡死
_ReceiveThread.Start();
```

**线程间通信：**

- 收包：后台线程 `Enqueue` → 主线程 `TryDequeue`，通过 `ConcurrentQueue`
- 发包：主线程 `lock (_SendLock)` 后写入 `_Stream`
- 停止信号：`volatile bool _IsRunning`，主线程写 `false`，后台线程读

**线程退出路径：**

| 场景 | 触发 | 后果 |
|------|------|------|
| 主动断开 | `Disconnect()` → `_Stream.Close()` | 后台线程 `Read()` 抛 `ObjectDisposedException` → finally → `OnDisconnected` |
| 服务端断开 | TCP 连接关闭 | 后台线程 `Read()` 返回 0 → `EndOfStreamException` → finally → `OnDisconnected` |

#### NetworkManager

普通 C# 类，整个网络层的唯一入口，由外部持有并驱动（无 MonoBehaviour 单例）。

**生命周期：**

| 方法 | 调用时机 |
|------|----------|
| `Initialize()` | 场景启动时，创建 NetworkClient 和 MessageDispatcher |
| `Tick()` | 每帧由 MonoBehaviour.Update() 调用，排空接收队列 |
| `Dispose()` | 场景卸载时，断开连接并清理资源 |

**主线程消息泵（Tick）：**

```
每帧执行：
1. 检查 _NotifyDisconnectOnMainThread 标志 → 触发 OnDisconnected 事件
2. while 循环排空 ConcurrentQueue → 逐个交给 MessageDispatcher.Dispatch()
```

使用 `while` 而非 `if` 确保一帧内处理完所有积压消息，避免延迟累积。

**断连通知的线程切换：** 后台线程触发 `HandleDisconnected` 仅设标志位 → 主线程 `Tick` 检测后触发事件，保证订阅者可安全调用 Unity API。

**公开 API：**

| 方法 | 说明 |
|------|------|
| `Initialize()` | 初始化内部组件 |
| `Connect(host, port)` | 建立 TCP 连接，启动后台接收线程 |
| `Disconnect()` | 关闭连接 |
| `Send<T>(type, message)` | 序列化并发送 protobuf 消息 |
| `RegisterHandler<T>(type, handler)` | 注册消息处理回调 |
| `UnregisterHandler(type)` | 移除回调 |
| `Dispose()` | 释放全部资源 |

### 3.4 完整数据流

```
发送路径：
业务层调用 Send<T>(type, msg)
  → IMessage.ToByteArray() 序列化
  → PacketWriter.WriteClientPacket() 编码为 [8B头 + 载荷]
  → lock(_SendLock) { stream.Write() }  主线程加锁写入
  → TCP → 服务端

接收路径：
服务端 → TCP
  → 后台线程 PacketReader.ReadServerPacket() 解码为 ServerPacket
  → ConcurrentQueue.Enqueue()
  → 主线程 Update() → TryDequeue()
  → MessageDispatcher.Dispatch()
  → Parser.ParseFrom() 反序列化为具体 Protobuf 类型
  → 业务层回调（主线程安全）
```

---

## 四、ECS 架构规划

### 4.1 设计目标

采用**轻量自研 ECS** 而非 Unity DOTS。理由：

- 项目为状态同步原型，DOTS 的 Burst/Job System 对网络同步无明显收益
- 自研 ECS 代码完全可控，学习曲线低
- 状态同步天然适配 ECS：服务端下发实体状态 → 客户端按 Component 更新

### 4.2 整体分层

```
┌─────────────────────────────────────────────────┐
│                Unity MonoBehaviour               │
│  GameWorld.cs — 持有 World，驱动 Update 循环      │
│  NetworkManager.cs — TCP 连接、收发包              │
└──────────────┬──────────────────┬────────────────┘
               │                  │
               ▼                  ▼
┌──────────────────────┐  ┌────────────────────────┐
│     ECS Core         │  │   Service Layer        │
│  World / Entity      │  │   NetworkManager       │
│  IComponent          │  │   (可扩展:              │
│  SystemBase          │  │    AssetService 等)     │
└──────────┬───────────┘  └────────────┬───────────┘
           │                           │
           ▼                           │
┌──────────────────────────────────────┘───────────┐
│                  Systems                          │
│  NetworkReceiveSystem ← 从 NetworkManager 读消息  │
│  NetworkSendSystem    → 向 NetworkManager 发消息  │
│  MovementSystem       — 纯 ECS 逻辑              │
│  RenderSyncSystem     — ECS → Unity GameObject    │
└───────────────────────────────────────────────────┘
```

**NetworkManager 定位：** 基础设施服务层，位于 ECS 之外。System 通过 `World.GetService<NetworkManager>()` 获取引用。

### 4.3 规划文件结构

```
Assets/Scripts/
├── _gen/                           # protobuf 自动生成
├── Network/                        # 基础设施层（已实现）
│   ├── NetworkManager.cs           # 普通 C# 类，Initialize/Tick/Dispose
│   ├── NetworkClient.cs
│   ├── PacketReader.cs
│   ├── PacketWriter.cs
│   ├── MessageDispatcher.cs
│   └── ServerPacket.cs
├── UI/                             # UI 层（已实现）
│   ├── UIManager.cs
│   ├── RoomUIBuilder.cs
│   └── RoomUIController.cs
├── ECS/
│   ├── Core/                       # ECS 框架内核
│   │   ├── IComponent.cs           # 组件标记接口
│   │   ├── Entity.cs               # 实体 = ID + 组件字典
│   │   ├── World.cs                # 实体容器 + 系统调度 + 服务定位
│   │   └── SystemBase.cs           # 系统基类
│   ├── Components/                 # 具体组件（纯数据）
│   │   ├── NetworkIdComponent.cs   # 网络实体标识
│   │   ├── TransformComponent.cs   # 位置、旋转
│   │   └── InputComponent.cs       # 玩家输入帧数据
│   └── Systems/                    # 具体系统（纯逻辑）
│       ├── NetworkReceiveSystem.cs  # 收包 → 创建/更新 Entity
│       ├── NetworkSendSystem.cs     # 采集输入 → 发包
│       ├── MovementSystem.cs        # 位移计算
│       └── RenderSyncSystem.cs      # ECS → Unity Transform
└── Game/
    └── GameWorld.cs                # MonoBehaviour，桥接 Unity 和 ECS
```

### 4.4 核心类设计

| 类 | 职责 |
|---|---|
| `IComponent` | 空标记接口，所有组件实现它 |
| `Entity` | int ID + `Dictionary<Type, IComponent>`，纯数据容器 |
| `World` | 管理所有 Entity 的创建/销毁/查询，持有 System 列表按序执行，提供 `GetService<T>()` 服务定位 |
| `SystemBase` | 抽象基类，提供 `World` 引用和 `Update(float deltaTime)` 虚方法 |
| `GameWorld` | MonoBehaviour，创建 `World` 实例，在 Unity `Update()` 中驱动 `World.Update()` |

### 4.5 System 执行顺序

```
每帧由 World 按顺序调用：
1. NetworkReceiveSystem   — 处理服务端数据，创建/更新 Entity
2. InputSystem            — 采集本地输入，写入 InputComponent
3. MovementSystem         — 游戏逻辑（位移、碰撞等）
4. NetworkSendSystem      — 将本帧输入发送给服务端
5. RenderSyncSystem       — 将 ECS 数据同步到 Unity GameObject
```

### 4.6 状态同步数据流

```
服务端 WorldState 包
  → NetworkManager.Update() 出队
  → NetworkReceiveSystem.Update()
      遍历同步数据，按 networkId 查找 Entity
      存在 → 更新 TransformComponent
      不存在 → World.CreateEntity() + 挂载组件
  → MovementSystem.Update()
      插值/预测平滑
  → RenderSyncSystem.Update()
      TransformComponent → GameObject.transform

本地玩家输入
  → InputSystem.Update() 采集按键 → InputComponent
  → NetworkSendSystem.Update() 读取 → 序列化为 PlayerInput → 发送
```

### 4.7 ECS Entity 与 Unity GameObject 的关系

- ECS 是**权威数据源**，GameObject 仅作为渲染表现层
- `RenderSyncSystem` 维护 `Entity → GameObject` 映射表
- 创建 Entity 时实例化 Prefab，销毁 Entity 时回收 GameObject
- 游戏逻辑只读写 Component，不直接操作 GameObject
