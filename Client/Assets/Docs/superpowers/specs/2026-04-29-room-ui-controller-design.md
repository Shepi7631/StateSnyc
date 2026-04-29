# Room UI Controller 设计文档

**日期：** 2026-04-29  
**项目：** StateSync Unity Client  
**范围：** 大厅与房间 UI 的 Controller 层及 Unity 场景 UI 结构

---

## 1. 背景与目标

客户端已有完整的网络层（`NetworkManager`、`MessageDispatcher`、Protobuf 消息）。本次目标是在此基础上实现玩家从登录、浏览大厅、创建/加入房间到等待游戏开始的完整 UI 流程。

---

## 2. 架构决策

采用**单一 `RoomUIController` MonoBehaviour + 面板 SetActive 切换**方案。  
三个面板（LoginPanel / LobbyPanel / RoomPanel）共存于同一 Canvas，同一时刻只有一个面板激活。不引入额外状态机或 UIManager，与当前项目规模匹配。

---

## 3. Unity 场景 UI 结构

### 3.1 层级结构

```
Canvas (Screen Space - Overlay)
├── LoginPanel
│   ├── Background (Image, 半透明深色)
│   ├── Title (Text: "StateSync")
│   ├── NicknameInput (InputField, placeholder: "输入昵称")
│   ├── HostInput (InputField, placeholder: "服务器地址", default: "127.0.0.1")
│   ├── PortInput (InputField, placeholder: "端口", default: "8080")
│   └── ConnectButton (Button: "连接")
│
├── LobbyPanel
│   ├── Background (Image)
│   ├── Title (Text: "大厅")
│   ├── QuickJoinGroup (HorizontalLayoutGroup)
│   │   ├── RoomIdInput (InputField, placeholder: "输入房间号")
│   │   └── JoinButton (Button: "加入")
│   ├── CreateButton (Button: "一键创建房间")
│   └── ErrorText (Text, 红色, 默认 inactive)
│
└── RoomPanel
    ├── Background (Image)
    ├── RoomIdText (Text: "房间：------")
    ├── PlayerCountText (Text: "等待玩家... 1 / 16")
    ├── WaitingText (Text: "等待其他玩家加入...")
    └── LeaveButton (Button: "离开房间")
```

### 3.2 Canvas 设置

- **Render Mode：** Screen Space - Overlay  
- **Canvas Scaler：** Scale With Screen Size，参考分辨率 1920×1080，Match 0.5

---

## 4. 消息流程

### 4.1 创建房间（两步）

```
Client                          Server
  │── CreateRoom{MaxPlayers=4} ──▶│
  │◀─ CreateRoom{RoomId} ─────────│  OnCreateRoomResponse
  │── JoinRoom{RoomId} ──────────▶│  （自动发送，不需要用户操作）
  │◀─ JoinRoom{RoomId,PlayerId,   │  OnJoinRoomResponse → 切到 RoomPanel
  │           PlayerIds} ─────────│
```

### 4.2 加入房间

```
Client                          Server
  │── JoinRoom{RoomId} ──────────▶│
  │◀─ JoinRoom{RoomId,PlayerId,   │  OnJoinRoomResponse → 切到 RoomPanel
  │           PlayerIds} ─────────│
```

### 4.3 他人加入（广播）

```
  │◀─ PlayerJoined{PlayerId,RoomId}│  OnPlayerJoinedResponse → 更新人数显示
```

---

## 5. 错误处理

| 场景 | ErrorCode | UI 行为 |
|---|---|---|
| 加入：房间不存在 | `RoomNotFound` | LobbyPanel ErrorText 显示"房间不存在" |
| 加入：房间已满 | `RoomFull` | ErrorText 显示"房间已满（最多 N 人）"，N 取 `ErrorParams[0]` |
| 加入：已在房间 | `RoomAlreadyJoined` | ErrorText 显示"已在房间中" |
| 创建/加入：无效输入 | `InvalidInput` | ErrorText 显示"操作失败，请重试" |
| 网络断线 | `OnDisconnected` | 强制切回 LoginPanel，清空状态 |
| 重复点击 | — | 发请求后立即禁用按钮，收到响应后恢复 |

ErrorText 通过 Coroutine 在 2 秒后自动隐藏。

---

## 6. Controller API

**文件：** `Assets/Scripts/UI/RoomUIController.cs`  
**命名空间：** `StateSync.Client.UI`

### 6.1 序列化字段（Inspector 绑定）

```csharp
// LoginPanel
[SerializeField] GameObject _LoginPanel
[SerializeField] InputField _NicknameInput
[SerializeField] InputField _HostInput
[SerializeField] InputField _PortInput
[SerializeField] Button     _ConnectButton

// LobbyPanel
[SerializeField] GameObject _LobbyPanel
[SerializeField] InputField _RoomIdInput
[SerializeField] Button     _JoinButton
[SerializeField] Button     _CreateButton
[SerializeField] Text       _LobbyErrorText

// RoomPanel
[SerializeField] GameObject _RoomPanel
[SerializeField] Text       _RoomIdText
[SerializeField] Text       _PlayerCountText
[SerializeField] Button     _LeaveButton
```

### 6.2 运行时状态

```csharp
string _nickname        // 本地昵称，仅用于 UI 显示
string _playerId        // 服务端分配（来自 JoinRoom 响应）
string _currentRoomId   // 当前所在房间 ID
int    _playerCount     // 当前房间人数
int    _maxPlayers      // 当前房间最大人数
```

### 6.3 方法列表

| 方法 | 触发时机 |
|---|---|
| `Start()` | 注册网络回调；注册 `OnDisconnected`；显示 LoginPanel |
| `OnDestroy()` | 注销所有网络回调 |
| `OnConnectClicked()` | 连接按钮点击；读取 Host/Port/Nickname；调用 `NetworkManager.Connect` |
| `OnCreateClicked()` | 创建按钮点击；禁用按钮；发 `CreateRoom{MaxPlayers=4}` |
| `OnJoinClicked()` | 加入按钮点击；校验 RoomIdInput 非空；禁用按钮；发 `JoinRoom{RoomId}` |
| `OnLeaveClicked()` | 离开按钮点击；发 `LeaveRoom`；切回 LobbyPanel |
| `OnCreateRoomResponse(...)` | 收到 CreateRoom 响应；成功则自动发 JoinRoom；失败则显示错误 |
| `OnJoinRoomResponse(...)` | 收到 JoinRoom 响应；成功则更新状态并切到 RoomPanel |
| `OnPlayerJoinedResponse(...)` | 收到 PlayerJoined 广播；更新 PlayerCountText |
| `OnDisconnected()` | 网络断线；重置状态；切回 LoginPanel |
| `ShowPanel(GameObject)` | 统一的面板切换，SetActive |
| `ShowLobbyError(string)` | 显示错误文字，Coroutine 2秒后隐藏 |
| `SetLobbyButtonsInteractable(bool)` | 统一控制 Join/Create 按钮可交互状态 |
| `UpdateRoomPanel()` | 刷新 RoomIdText 和 PlayerCountText |

---

## 7. 文件清单

| 文件 | 说明 |
|---|---|
| `Assets/Scripts/UI/RoomUIController.cs` | 唯一的 UI Controller |

Unity 场景的 UI 层级由实现阶段在 Inspector 中手动拼凑或通过代码创建。
