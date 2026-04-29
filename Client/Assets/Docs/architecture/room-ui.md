# Room UI 架构文档

## 概述

Room UI 模块负责呈现三个面板（登录、大厅、房间等待），并将用户操作桥接到网络层。整个模块以纯 C# 类实现，不依赖 Unity MonoBehaviour 单例，生命周期由外部的 `UIManager` 统一管理。

---

## 文件结构

```
Assets/Scripts/
├── Network/
│   ├── NetworkClient.cs       # TCP 连接、收发、后台接收线程
│   └── NetworkManager.cs      # 对外暴露 Connect / Send / Tick / Dispose
└── UI/
    ├── UIManager.cs           # 协调 NetworkManager + RoomUIController 的入口
    ├── RoomUIBuilder.cs       # 运行时以纯代码构建 Canvas 层级，返回引用集合
    └── RoomUIController.cs    # 面板切换、按钮回调、网络响应处理
```

---

## 架构设计

### 生命周期

```
MonoBehaviour（场景入口）
    └── UIManager
            ├── NetworkManager   ← Initialize() / Tick() / Dispose()
            └── RoomUIController ← Initialize() / Tick(deltaTime) / Dispose()
```

`UIManager` 在 `Initialize()` 时调用 `RoomUIBuilder.Build()` 获取 `RoomUIReferences`，再将引用注入 `RoomUIController`。`MonoBehaviour.Update()` 每帧调用 `UIManager.Tick()`，`OnDestroy()` 调用 `UIManager.Dispose()`。

### 与原 MonoBehaviour 方案的区别

| 方面 | 旧方案 | 现方案 |
|---|---|---|
| NetworkManager | MonoBehaviour 单例（DontDestroyOnLoad） | 普通 C# 类，由外部持有 |
| RoomUIController | MonoBehaviour，依赖 `Start()` / `OnDestroy()` | 普通 C# 类，显式 `Initialize` / `Dispose` |
| RoomUIBuilder | MonoBehaviour，`Awake()` 构建后 `Destroy(this)` | 普通 C# 类，`Build()` 返回引用对象 |
| UI 引用传递 | 通过 `AddComponent` 和多参数 `Initialize` | 通过 `RoomUIReferences` 值对象集中传递 |

---

## RoomUIBuilder

`RoomUIBuilder.Build()` 以纯代码创建 Canvas 及三个面板，返回 `RoomUIReferences`。

### 面板布局（anchorMin / anchorMax，Y 轴从下到上）

**LoginPanel**

| 控件 | anchorMin | anchorMax |
|---|---|---|
| TitleText "StateSync" | (0, 0.72) | (1, 0.88) |
| NicknameInput | (0.20, 0.58) | (0.80, 0.68) |
| HostInput（默认 127.0.0.1） | (0.20, 0.44) | (0.80, 0.54) |
| PortInput（默认 8080） | (0.20, 0.30) | (0.80, 0.40) |
| ConnectButton | (0.30, 0.16) | (0.70, 0.26) |
| LoginErrorText（默认隐藏） | (0.10, 0.04) | (0.90, 0.13) |

**LobbyPanel**

| 控件 | anchorMin | anchorMax |
|---|---|---|
| TitleText "大厅" | (0, 0.72) | (1, 0.88) |
| RoomIdInput | (0.10, 0.54) | (0.62, 0.64) |
| JoinButton | (0.64, 0.54) | (0.90, 0.64) |
| CreateButton | (0.20, 0.38) | (0.80, 0.48) |
| LobbyErrorText（默认隐藏） | (0.10, 0.24) | (0.90, 0.34) |

**RoomPanel**

| 控件 | anchorMin | anchorMax |
|---|---|---|
| RoomIdText | (0, 0.65) | (1, 0.75) |
| PlayerCountText | (0, 0.52) | (1, 0.62) |
| WaitingHint | (0, 0.40) | (1, 0.50) |
| LeaveButton | (0.30, 0.20) | (0.70, 0.30) |

Canvas 使用 `ScaleWithScreenSize`，参考分辨率 1920×1080，`matchWidthOrHeight = 0.5`。字体统一使用 `Arial.ttf`（Unity 2021 内置）。

---

## RoomUIController

### 面板状态机

```
LoginPanel  ──[连接成功]──►  LobbyPanel  ──[加入/创建成功]──►  RoomPanel
                                 ▲                                   │
                                 └──────────[离开房间]───────────────┘
任意面板  ──[断线]──►  LoginPanel
```

### 错误提示

- **LoginErrorText**：连接失败（TCP 拒绝或超时）时显示，3 秒后自动隐藏
- **LobbyErrorText**：加入/创建失败时显示，2 秒后自动隐藏；错误文本通过 `ErrorCode` 映射

```
RoomNotFound      → "房间不存在"
RoomFull          → "房间已满（最多 N 人）"
RoomAlreadyJoined → "已在房间中"
其他              → "操作失败，请重试"
```

计时器在 `Tick(deltaTime)` 中递减，不使用协程。

### 网络消息注册

| MessageType | 注册 Handler |
|---|---|
| JoinRoom | OnJoinRoomResponse |
| CreateRoom | OnCreateRoomResponse |
| PlayerJoined | OnPlayerJoinedResponse |

`Dispose()` 时移除全部监听，防止野引用。

### 创建房间流程

1. 点击"创建房间" → 发送 `CreateRoom { MaxPlayers = 4 }`
2. 收到 `OnCreateRoomResponse`（Success）→ 自动发送 `JoinRoom { RoomId }`
3. 收到 `OnJoinRoomResponse`（Success）→ 切换到 RoomPanel

---

## 连接错误处理

| 异常类型 | 处理位置 | 行为 |
|---|---|---|
| `SocketException` | `RoomUIController.OnConnectClicked` | 显示 LoginErrorText，不跳转面板 |
| 其他 `Exception`（`Connect`） | 同上 | 同上 |
| `InvalidDataException`（`ReceiveLoop`） | `NetworkClient.ReceiveLoop` | 打 Warning 日志，触发 `OnDisconnected` |
| `IOException` | 同上 | 同上 |
| `EndOfStreamException` | 同上 | 正常断线日志 |

`InvalidDataException` 通常在连接到非 StateSync TCP 服务（如 HTTP 服务器）时触发，现象：`Invalid payload length: 825110831`（HTTP/1.1 响应头的 ASCII 字节被误读为帧长度）。

NetworkManager / NetworkClient 的详细设计见 [client-architecture.md](client-architecture.md)。

---

## 已知限制

| 问题 | 说明 |
|---|---|
| LeaveRoom 无服务端处理 | 客户端发送空包，服务端返回 `InvalidInput`，客户端无 handler，不影响流程 |
| PlayerCount 来源 | 仅依赖 JoinRoom 响应中的 `PlayerIds.Count` 和 `PlayerJoined` 推送递增，无完整 PlayerLeft 处理 |
