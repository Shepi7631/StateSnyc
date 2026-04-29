# Room UI Controller Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一套完整的 Unity 房间 UI，包含登录面板、大厅面板、房间等待面板，以及程序化 UI 构建脚本。

**Architecture:** 单一 `RoomUIController` MonoBehaviour 管理三个面板的切换与网络消息处理；`RoomUIBuilder` 在运行时以纯代码方式构建全部 Unity UI 层级，构建完毕后自毁。两者解耦，Controller 也可通过 Inspector 手动绑定引用。

**Tech Stack:** Unity UGUI (UnityEngine.UI)、Protobuf (StateSync.Shared)、StateSync.Client.Network (NetworkManager)

---

## 文件清单

| 操作 | 路径 | 说明 |
|---|---|---|
| 新建 | `Assets/Scripts/UI/RoomUIController.cs` | UI 逻辑与网络回调 |
| 新建 | `Assets/Scripts/UI/RoomUIBuilder.cs` | 程序化构建 Canvas 层级 |

---

## Task 1：RoomUIController — 骨架与面板切换

**Files:**
- Create: `Assets/Scripts/UI/RoomUIController.cs`

- [ ] **Step 1: 创建文件，写入骨架代码**

```csharp
using System.Collections;
using StateSync.Client.Network;
using StateSync.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace StateSync.Client.UI
{
    public class RoomUIController : MonoBehaviour
    {
        private GameObject _LoginPanel;
        private InputField _NicknameInput;
        private InputField _HostInput;
        private InputField _PortInput;
        private Button     _ConnectButton;

        private GameObject _LobbyPanel;
        private InputField _RoomIdInput;
        private Button     _JoinButton;
        private Button     _CreateButton;
        private Text       _LobbyErrorText;

        private GameObject _RoomPanel;
        private Text       _RoomIdText;
        private Text       _PlayerCountText;
        private Button     _LeaveButton;

        private string _nickname;
        private string _playerId;
        private string _currentRoomId;
        private int    _playerCount;
        private Coroutine _errorCoroutine;

        public void Initialize(
            GameObject loginPanel, InputField nicknameInput, InputField hostInput,
            InputField portInput, Button connectButton,
            GameObject lobbyPanel, InputField roomIdInput, Button joinButton,
            Button createButton, Text lobbyErrorText,
            GameObject roomPanel, Text roomIdText, Text playerCountText, Button leaveButton)
        {
            _LoginPanel    = loginPanel;
            _NicknameInput = nicknameInput;
            _HostInput     = hostInput;
            _PortInput     = portInput;
            _ConnectButton = connectButton;

            _LobbyPanel    = lobbyPanel;
            _RoomIdInput   = roomIdInput;
            _JoinButton    = joinButton;
            _CreateButton  = createButton;
            _LobbyErrorText = lobbyErrorText;

            _RoomPanel        = roomPanel;
            _RoomIdText       = roomIdText;
            _PlayerCountText  = playerCountText;
            _LeaveButton      = leaveButton;
        }

        private void Start()
        {
            _ConnectButton.onClick.AddListener(OnConnectClicked);
            _JoinButton.onClick.AddListener(OnJoinClicked);
            _CreateButton.onClick.AddListener(OnCreateClicked);
            _LeaveButton.onClick.AddListener(OnLeaveClicked);

            NetworkManager.Instance.RegisterHandler<JoinRoom>(
                MessageType.JoinRoom, OnJoinRoomResponse);
            NetworkManager.Instance.RegisterHandler<CreateRoom>(
                MessageType.CreateRoom, OnCreateRoomResponse);
            NetworkManager.Instance.RegisterHandler<PlayerJoined>(
                MessageType.PlayerJoined, OnPlayerJoinedResponse);
            NetworkManager.Instance.OnDisconnected += OnDisconnected;

            ShowPanel(_LoginPanel);
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.UnregisterHandler(MessageType.JoinRoom);
                NetworkManager.Instance.UnregisterHandler(MessageType.CreateRoom);
                NetworkManager.Instance.UnregisterHandler(MessageType.PlayerJoined);
                NetworkManager.Instance.OnDisconnected -= OnDisconnected;
            }
        }

        private void ShowPanel(GameObject panel)
        {
            _LoginPanel.SetActive(panel == _LoginPanel);
            _LobbyPanel.SetActive(panel == _LobbyPanel);
            _RoomPanel.SetActive(panel == _RoomPanel);
        }

        private void SetLobbyButtonsInteractable(bool interactable)
        {
            _JoinButton.interactable   = interactable;
            _CreateButton.interactable = interactable;
        }

        private void UpdateRoomPanel()
        {
            _RoomIdText.text      = $"房间：{_currentRoomId}";
            _PlayerCountText.text = $"等待玩家... {_playerCount} 人";
        }

        private void ShowLobbyError(string message)
        {
            if (_errorCoroutine != null) StopCoroutine(_errorCoroutine);
            _errorCoroutine = StartCoroutine(ShowErrorCoroutine(message));
        }

        private IEnumerator ShowErrorCoroutine(string message)
        {
            _LobbyErrorText.text = message;
            _LobbyErrorText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            _LobbyErrorText.gameObject.SetActive(false);
        }

        // ── Button handlers（Task 2 填充）──────────────────────────────────
        private void OnConnectClicked() { }
        private void OnCreateClicked()  { }
        private void OnJoinClicked()    { }
        private void OnLeaveClicked()   { }

        // ── Network response handlers（Task 2 填充）────────────────────────
        private void OnCreateRoomResponse(CreateRoom msg, ErrorCode err, int[] p)   { }
        private void OnJoinRoomResponse(JoinRoom msg, ErrorCode err, int[] p)       { }
        private void OnPlayerJoinedResponse(PlayerJoined msg, ErrorCode err, int[] p) { }
        private void OnDisconnected() { }
    }
}
```

- [ ] **Step 2: 在 Unity 中确认文件无编译错误**

打开 Unity Editor，等待脚本编译完成，Console 无红色错误即可。

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/RoomUIController.cs
git commit -m "feat: add RoomUIController skeleton with panel switching"
```

---

## Task 2：RoomUIController — 按钮处理 & 网络逻辑

**Files:**
- Modify: `Assets/Scripts/UI/RoomUIController.cs`

- [ ] **Step 1: 填充按钮回调**

将 Task 1 中的空方法替换为以下完整实现：

```csharp
private void OnConnectClicked()
{
    string nickname = _NicknameInput.text.Trim();
    string host     = _HostInput.text.Trim();
    if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(host) ||
        !int.TryParse(_PortInput.text.Trim(), out int port))
        return;

    _nickname = nickname;
    _ConnectButton.interactable = false;
    NetworkManager.Instance.Connect(host, port);
    _ConnectButton.interactable = true;
    ShowPanel(_LobbyPanel);
}

private void OnCreateClicked()
{
    SetLobbyButtonsInteractable(false);
    NetworkManager.Instance.Send(MessageType.CreateRoom, new CreateRoom { MaxPlayers = 4 });
}

private void OnJoinClicked()
{
    string roomId = _RoomIdInput.text.Trim();
    if (string.IsNullOrEmpty(roomId)) return;

    SetLobbyButtonsInteractable(false);
    NetworkManager.Instance.Send(MessageType.JoinRoom, new JoinRoom { RoomId = roomId });
}

private void OnLeaveClicked()
{
    // 服务端暂未实现 LeaveRoom 处理，发送空包占位；本地立即切回大厅
    NetworkManager.Instance.Send(MessageType.LeaveRoom, new JoinRoom());
    _currentRoomId = null;
    _playerId      = null;
    _playerCount   = 0;
    ShowPanel(_LobbyPanel);
}
```

- [ ] **Step 2: 填充网络响应处理**

```csharp
private void OnCreateRoomResponse(CreateRoom msg, ErrorCode err, int[] errorParams)
{
    if (err != ErrorCode.Success)
    {
        SetLobbyButtonsInteractable(true);
        ShowLobbyError("创建失败，请重试");
        return;
    }
    // 创建成功后自动加入该房间
    NetworkManager.Instance.Send(MessageType.JoinRoom, new JoinRoom { RoomId = msg.RoomId });
}

private void OnJoinRoomResponse(JoinRoom msg, ErrorCode err, int[] errorParams)
{
    SetLobbyButtonsInteractable(true);
    if (err != ErrorCode.Success)
    {
        ShowLobbyError(err switch
        {
            ErrorCode.RoomNotFound      => "房间不存在",
            ErrorCode.RoomFull          => $"房间已满（最多 {(errorParams.Length > 0 ? errorParams[0] : 0)} 人）",
            ErrorCode.RoomAlreadyJoined => "已在房间中",
            _                           => "操作失败，请重试"
        });
        return;
    }

    _playerId      = msg.PlayerId;
    _currentRoomId = msg.RoomId;
    _playerCount   = msg.PlayerIds.Count;
    UpdateRoomPanel();
    ShowPanel(_RoomPanel);
}

private void OnPlayerJoinedResponse(PlayerJoined msg, ErrorCode err, int[] errorParams)
{
    if (msg.RoomId != _currentRoomId) return;
    _playerCount++;
    UpdateRoomPanel();
}

private void OnDisconnected()
{
    _currentRoomId = null;
    _playerId      = null;
    _playerCount   = 0;
    ShowPanel(_LoginPanel);
}
```

- [ ] **Step 3: Unity 编译确认无错误**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/RoomUIController.cs
git commit -m "feat: implement button handlers and network response logic in RoomUIController"
```

---

## Task 3：RoomUIBuilder — 程序化构建 UI 层级

**Files:**
- Create: `Assets/Scripts/UI/RoomUIBuilder.cs`

- [ ] **Step 1: 创建文件**

```csharp
using StateSync.Client.Network;
using UnityEngine;
using UnityEngine.UI;

namespace StateSync.Client.UI
{
    public class RoomUIBuilder : MonoBehaviour
    {
        private void Awake()
        {
            EnsureEventSystem();
            Canvas canvas = CreateCanvas();

            // ── 三个面板 ──────────────────────────────────────────────────
            GameObject loginPanel = CreatePanel(canvas.transform, "LoginPanel",
                new Color(0.08f, 0.08f, 0.16f, 0.97f));
            GameObject lobbyPanel = CreatePanel(canvas.transform, "LobbyPanel",
                new Color(0.08f, 0.10f, 0.14f, 0.97f));
            GameObject roomPanel  = CreatePanel(canvas.transform, "RoomPanel",
                new Color(0.06f, 0.10f, 0.16f, 0.97f));

            // ── LoginPanel ────────────────────────────────────────────────
            CreateText(loginPanel.transform, "TitleText", "StateSync",
                40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.72f), new Vector2(1f, 0.88f));

            InputField nicknameInput = CreateInputField(loginPanel.transform,
                "NicknameInput", "输入昵称",
                new Vector2(0.20f, 0.58f), new Vector2(0.80f, 0.68f));

            InputField hostInput = CreateInputField(loginPanel.transform,
                "HostInput", "服务器地址",
                new Vector2(0.20f, 0.44f), new Vector2(0.80f, 0.54f));
            hostInput.text = "127.0.0.1";

            InputField portInput = CreateInputField(loginPanel.transform,
                "PortInput", "端口",
                new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.40f));
            portInput.text = "8080";

            Button connectButton = CreateButton(loginPanel.transform,
                "ConnectButton", "连 接",
                new Vector2(0.30f, 0.16f), new Vector2(0.70f, 0.26f));

            // ── LobbyPanel ────────────────────────────────────────────────
            CreateText(lobbyPanel.transform, "TitleText", "大  厅",
                40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.72f), new Vector2(1f, 0.88f));

            InputField roomIdInput = CreateInputField(lobbyPanel.transform,
                "RoomIdInput", "输入房间号",
                new Vector2(0.10f, 0.54f), new Vector2(0.62f, 0.64f));

            Button joinButton = CreateButton(lobbyPanel.transform,
                "JoinButton", "加入",
                new Vector2(0.64f, 0.54f), new Vector2(0.90f, 0.64f));

            Button createButton = CreateButton(lobbyPanel.transform,
                "CreateButton", "一键创建房间",
                new Vector2(0.20f, 0.38f), new Vector2(0.80f, 0.48f));

            Text lobbyErrorText = CreateText(lobbyPanel.transform,
                "ErrorText", "",
                18, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.34f));
            lobbyErrorText.color = new Color(1f, 0.35f, 0.35f);
            lobbyErrorText.gameObject.SetActive(false);

            // ── RoomPanel ─────────────────────────────────────────────────
            Text roomIdText = CreateText(roomPanel.transform,
                "RoomIdText", "房间：------",
                26, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.65f), new Vector2(1f, 0.75f));

            Text playerCountText = CreateText(roomPanel.transform,
                "PlayerCountText", "等待玩家... 0 人",
                22, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.52f), new Vector2(1f, 0.62f));

            CreateText(roomPanel.transform, "WaitingHint",
                "等待其他玩家加入...",
                18, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.40f), new Vector2(1f, 0.50f));

            Button leaveButton = CreateButton(roomPanel.transform,
                "LeaveButton", "离开房间",
                new Vector2(0.30f, 0.20f), new Vector2(0.70f, 0.30f));

            // ── 绑定 Controller ───────────────────────────────────────────
            RoomUIController controller =
                canvas.gameObject.AddComponent<RoomUIController>();

            controller.Initialize(
                loginPanel, nicknameInput, hostInput, portInput, connectButton,
                lobbyPanel, roomIdInput, joinButton, createButton, lobbyErrorText,
                roomPanel, roomIdText, playerCountText, leaveButton);

            loginPanel.SetActive(true);
            lobbyPanel.SetActive(false);
            roomPanel.SetActive(false);

            Destroy(this);
        }

        // ── 辅助：EventSystem ─────────────────────────────────────────────

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── 辅助：Canvas ──────────────────────────────────────────────────

        private Canvas CreateCanvas()
        {
            GameObject go = new GameObject("RoomUI");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ── 辅助：Panel ───────────────────────────────────────────────────

        private GameObject CreatePanel(Transform parent, string name, Color bgColor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = bgColor;
            return go;
        }

        // ── 辅助：Text ────────────────────────────────────────────────────

        private Text CreateText(Transform parent, string name, string content,
            int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text t = go.AddComponent<Text>();
            t.text      = content;
            t.fontSize  = fontSize;
            t.alignment = alignment;
            t.color     = Color.white;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        // ── 辅助：InputField ──────────────────────────────────────────────

        private InputField CreateInputField(Transform parent, string name,
            string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            // 背景容器
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.28f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Placeholder
            GameObject pGo = new GameObject("Placeholder");
            pGo.transform.SetParent(go.transform, false);
            SetFullStretch(pGo.AddComponent<RectTransform>(), new Vector2(8, 4), new Vector2(-8, -4));
            Text pText = pGo.AddComponent<Text>();
            pText.text      = placeholder;
            pText.color     = new Color(0.65f, 0.65f, 0.65f, 0.9f);
            pText.font      = font;
            pText.fontSize  = 18;
            pText.fontStyle = FontStyle.Italic;

            // 输入文字
            GameObject tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            SetFullStretch(tGo.AddComponent<RectTransform>(), new Vector2(8, 4), new Vector2(-8, -4));
            Text iText = tGo.AddComponent<Text>();
            iText.color    = Color.white;
            iText.font     = font;
            iText.fontSize = 18;

            InputField field = go.AddComponent<InputField>();
            field.textComponent = iText;
            field.placeholder   = pText;
            return field;
        }

        // ── 辅助：Button ──────────────────────────────────────────────────

        private Button CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.47f, 0.87f);

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.28f, 0.57f, 0.97f);
            cb.pressedColor     = new Color(0.10f, 0.30f, 0.65f);
            cb.disabledColor    = new Color(0.40f, 0.40f, 0.40f);
            btn.colors = cb;

            // 文字
            GameObject tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            SetFullStretch(tGo.AddComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            Text t = tGo.AddComponent<Text>();
            t.text      = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = Color.white;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = 20;

            return btn;
        }

        // ── 辅助：RectTransform ───────────────────────────────────────────

        private static void SetFullStretch(RectTransform rt,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
```

- [ ] **Step 2: Unity 编译确认无错误**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/RoomUIBuilder.cs
git commit -m "feat: add RoomUIBuilder for programmatic Unity UI construction"
```

---

## Task 4：场景接入与手动验证

**Files:**
- 无代码变更，Unity 场景操作

- [ ] **Step 1: 在场景中放置 Builder**

在 Unity Hierarchy 窗口：
1. 右键 → Create Empty，命名为 `UIBootstrap`
2. 在 Inspector 中点击 Add Component → 搜索 `RoomUIBuilder`，添加

- [ ] **Step 2: 确认 NetworkManager 存在**

场景中需要有一个挂载了 `NetworkManager` 的 GameObject（它是 DontDestroyOnLoad 单例）。若无，创建 Empty → 命名 `NetworkManager` → Add Component → `NetworkManager`。

- [ ] **Step 3: 运行场景，验证 LoginPanel**

点击 Play：
- 应出现深色背景的 LoginPanel，包含"StateSync"标题、昵称输入框、地址框（默认 127.0.0.1）、端口框（默认 8080）、连接按钮
- Hierarchy 中应能看到 `RoomUI/LoginPanel`、`RoomUI/LobbyPanel`、`RoomUI/RoomPanel`

- [ ] **Step 4: 验证连接与大厅切换**

启动服务端后，在 LoginPanel 输入任意昵称，点击"连接"：
- 应切换到 LobbyPanel（"大厅"标题，房间号输入框、加入按钮、创建按钮）

- [ ] **Step 5: 验证创建房间**

在 LobbyPanel 点击"一键创建房间"：
- 按钮短暂变灰（不可点击）
- 成功后切换到 RoomPanel，显示 6 位房间号和"等待玩家... 1 人"

- [ ] **Step 6: 验证加入房间**

用第二个客户端实例（或另一台机器）输入同一房间号点击"加入"：
- 两个客户端的 RoomPanel 均显示"等待玩家... 2 人"

- [ ] **Step 7: 验证错误提示**

输入不存在的房间号点击"加入"：
- LobbyPanel 底部出现红色"房间不存在"，2 秒后自动消失
- 按钮恢复可点击

- [ ] **Step 8: 验证离开房间**

在 RoomPanel 点击"离开房间"：
- 切回 LobbyPanel，状态清空

- [ ] **Step 9: 最终 Commit**

```bash
git add Assets/Scripts/UI/
git commit -m "feat: complete Room UI — controller and programmatic builder"
```

---

## 已知限制

| 问题 | 说明 |
|---|---|
| LeaveRoom 无服务端处理 | 客户端发送空包，服务端返回 `InvalidInput`，客户端无 handler 仅打 warning，不影响流程 |
| MaxPlayers 不显示 | 服务端 JoinRoom/CreateRoom 响应均不含 MaxPlayers，PlayerCountText 只显示当前人数 |
| 内置字体 | 使用 `LegacyRuntime.ttf`（Unity 2021+），旧版 Unity 需改为 `Arial.ttf` |
