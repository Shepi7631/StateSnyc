# WorldManager Root + UIManager Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将客户端改为单一持久化根节点（`WorldManager`）驱动：`WorldManager` 是唯一 `MonoBehaviour`，并由它管理 `NetworkManager`、`UIManager` 等纯 C# 管理器生命周期。

**Architecture:** 在场景中仅保留一个 `WorldManager` 组件并 `DontDestroyOnLoad`。`WorldManager` 在 `Awake/Update/OnDestroy` 中转发生命周期到纯类管理器；`NetworkManager` 从 Unity 组件重构为普通类；新增 `UIManager` 负责创建/销毁 UI 和连接 UI 控制器到网络层。`RoomUIBuilder`/`RoomUIController` 迁移为纯类，不再继承 `MonoBehaviour`。

**Tech Stack:** Unity (C#), UnityEngine.SceneManagement, Unity UGUI (`UnityEngine.UI`), Protobuf (`StateSync.Shared`)

---

## File Structure (lock this before coding)

| Operation | Path | Responsibility |
|---|---|---|
| Create | `Assets\Scripts\World\WorldManager.cs` | 唯一 MonoBehaviour 根，单例、防重复、持久化、生命周期编排 |
| Create | `Assets\Scripts\UI\UIManager.cs` | UI 生命周期管理（创建 Canvas、绑定控制器、清理） |
| Modify | `Assets\Scripts\Network\NetworkManager.cs` | 从 MonoBehaviour 改为纯类，暴露 `Initialize/Tick/Dispose` |
| Modify | `Assets\Scripts\UI\RoomUIBuilder.cs` | 改为纯构建器类（不再 `MonoBehaviour`，提供 `Build()`） |
| Modify | `Assets\Scripts\UI\RoomUIController.cs` | 改为纯控制器类（不再 `MonoBehaviour`，提供 `Initialize/Dispose`） |
| Modify | `Assets\Scenes\SampleScene.unity` | `ManagerContainer` 挂载脚本从 `NetworkManager` 切到 `WorldManager` |

---

### Task 1: Baseline safety checkpoint

**Files:**
- Read only: `Assets\Scripts\Network\NetworkManager.cs`, `Assets\Scripts\UI\RoomUIBuilder.cs`, `Assets\Scripts\UI\RoomUIController.cs`, `Assets\Scenes\SampleScene.unity`

- [ ] **Step 1: 记录当前 MonoBehaviour 分布（只应看到 3 个）**

Run: `rg -n ":\s*MonoBehaviour" Assets\Scripts\**\*.cs`  
Expected: `NetworkManager`、`RoomUIBuilder`、`RoomUIController`。

- [ ] **Step 2: 记录场景当前 manager 挂载点**

Run: `rg -n "ManagerContainer|d09846a1ae9e1654c86a31bc08db98d3" Assets\Scenes\SampleScene.unity`  
Expected: `ManagerContainer` 节点当前绑定 `NetworkManager` 脚本 GUID。

- [ ] **Step 3: Commit（checkpoint）**

```bash
git add -A
git commit -m "chore: checkpoint before world-manager refactor"
```

---

### Task 2: Introduce persistent WorldManager root

**Files:**
- Create: `Assets\Scripts\World\WorldManager.cs`
- Modify: `Assets\Scenes\SampleScene.unity`

- [ ] **Step 1: 新建 `WorldManager` 骨架（唯一 MonoBehaviour）**

实现要点（写入代码）：
```csharp
public sealed class WorldManager : MonoBehaviour
{
    private static WorldManager _instance;
    private NetworkManager _networkManager;
    private UIManager _uiManager;

    private void Awake() { /* singleton guard + DontDestroyOnLoad + construct/init managers */ }
    private void Update() { /* _networkManager.Tick(); */ }
    private void OnDestroy() { /* dispose managers in reverse order */ }
}
```

- [ ] **Step 2: 在 `Awake` 实现持久化单例保护**

要求：
1. 重复实例时 `Destroy(gameObject)` 并返回。  
2. 首实例执行 `DontDestroyOnLoad(gameObject)`。  
3. 明确初始化顺序：先 `NetworkManager`，后 `UIManager`（UI 需要网络依赖）。

- [ ] **Step 3: 修改 `SampleScene.unity`，把 `ManagerContainer` 的脚本引用从 `NetworkManager` 改为 `WorldManager`**

实现方式：
1. 让 Unity 生成 `WorldManager.cs.meta`。  
2. 用新 GUID 替换 `ManagerContainer` 上旧 GUID（`d09846a1ae9e1654c86a31bc08db98d3`）。

- [ ] **Step 4: 验证场景绑定**

Run: `rg -n "ManagerContainer|m_Script" Assets\Scenes\SampleScene.unity`  
Expected: `ManagerContainer` 仅绑定 `WorldManager` GUID，不再绑定 `NetworkManager` GUID。

- [ ] **Step 5: Commit**

```bash
git add Assets\Scripts\World\WorldManager.cs Assets\Scripts\World\WorldManager.cs.meta Assets\Scenes\SampleScene.unity
git commit -m "feat: add persistent WorldManager root and wire scene entry"
```

---

### Task 3: Refactor NetworkManager into plain class

**Files:**
- Modify: `Assets\Scripts\Network\NetworkManager.cs`

- [ ] **Step 1: 去除 MonoBehaviour/Unity lifecycle 依赖**

改造目标：
1. 删除 `: MonoBehaviour`、`Awake/Update/OnDestroy`。  
2. 删除静态 `Instance`。  
3. 保留网络 API：`Connect`、`Disconnect`、`Send`、`RegisterHandler`、`UnregisterHandler`、`OnDisconnected`。

- [ ] **Step 2: 增加显式生命周期 API**

实现并在类内新增：
```csharp
public void Initialize();
public void Tick();
public void Dispose();
```
`Tick()` 负责处理 `TryDequeue` + `Dispatch`；`Dispose()` 负责断线、解绑回调、清理 dispatcher。

- [ ] **Step 3: 调整断线通知线程切回机制保持不变**

要求：保留 `_NotifyDisconnectOnMainThread`，由 `Tick()` 驱动主线程事件派发，避免行为回归。

- [ ] **Step 4: 验证编译引用**

Run: `rg -n "NetworkManager\.Instance" Assets\Scripts\**\*.cs`  
Expected: 无结果（后续由 `UIManager`/`WorldManager` 注入实例）。

- [ ] **Step 5: Commit**

```bash
git add Assets\Scripts\Network\NetworkManager.cs
git commit -m "refactor: convert NetworkManager to plain lifecycle-managed class"
```

---

### Task 4: Introduce UIManager and migrate UI builder/controller to plain classes

**Files:**
- Create: `Assets\Scripts\UI\UIManager.cs`
- Modify: `Assets\Scripts\UI\RoomUIBuilder.cs`
- Modify: `Assets\Scripts\UI\RoomUIController.cs`

- [ ] **Step 1: 将 `RoomUIBuilder` 改成纯类并暴露构建结果**

改造目标：
1. 删除 `: MonoBehaviour`、`Awake()`、`Destroy(this)`。  
2. 提供 `Build()` 方法返回 UI 引用对象（login/lobby/room 及控件引用）。  
3. 继续负责 `EventSystem` 与 Canvas 创建。

- [ ] **Step 2: 将 `RoomUIController` 改成纯类**

改造目标：
1. 删除 `: MonoBehaviour`、`Start()`、`OnDestroy()`、Coroutine 依赖。  
2. 替换 Coroutine 错误提示为时间戳驱动逻辑：`Tick(float deltaTime)` 中控制 2 秒隐藏。  
3. 提供 `Initialize(...)`、`Dispose()`、`Tick(float deltaTime)`。

- [ ] **Step 3: 新建 `UIManager` 统一 UI 生命周期**

`UIManager` 责任：
1. 构建 UI（调用 `RoomUIBuilder.Build()`）。  
2. 创建并初始化 `RoomUIController`（注入 `NetworkManager` 实例）。  
3. 暴露 `Tick(float deltaTime)` 与 `Dispose()` 给 `WorldManager` 调用。

- [ ] **Step 4: 验证 MonoBehaviour 收敛**

Run: `rg -n ":\s*MonoBehaviour" Assets\Scripts\**\*.cs`  
Expected: 仅 `WorldManager` 一处匹配。

- [ ] **Step 5: Commit**

```bash
git add Assets\Scripts\UI\UIManager.cs Assets\Scripts\UI\RoomUIBuilder.cs Assets\Scripts\UI\RoomUIController.cs
git commit -m "feat: add UIManager and convert UI stack to plain classes"
```

---

### Task 5: Wire manager ownership in WorldManager

**Files:**
- Modify: `Assets\Scripts\World\WorldManager.cs`

- [ ] **Step 1: 完成依赖装配**

在 `Awake()` 内：
1. `_networkManager = new NetworkManager(); _networkManager.Initialize();`  
2. `_uiManager = new UIManager(_networkManager); _uiManager.Initialize();`

- [ ] **Step 2: 完成每帧驱动与销毁顺序**

在 `Update()`：
1. `_networkManager.Tick();`  
2. `_uiManager.Tick(Time.deltaTime);`

在 `OnDestroy()`（逆序）：
1. `_uiManager?.Dispose();`  
2. `_networkManager?.Dispose();`

- [ ] **Step 3: 验证 ownership 约束**

Run: `rg -n "new NetworkManager|new UIManager|DontDestroyOnLoad" Assets\Scripts\**\*.cs`  
Expected: 仅 `WorldManager` 执行 manager 构造与 `DontDestroyOnLoad`。

- [ ] **Step 4: Commit**

```bash
git add Assets\Scripts\World\WorldManager.cs
git commit -m "refactor: make WorldManager the single owner of all managers"
```

---

### Task 6: End-to-end verification

**Files:**
- Validate: `Assets\Scripts\**\*.cs`, `Assets\Scenes\SampleScene.unity`

- [ ] **Step 1: 代码结构验证（CLI）**

Run:
```bash
rg -n ":\s*MonoBehaviour" Assets\Scripts\**\*.cs
rg -n "DontDestroyOnLoad" Assets\Scripts\**\*.cs
rg -n "NetworkManager\.Instance" Assets\Scripts\**\*.cs
```
Expected:
1. `MonoBehaviour` 仅 `WorldManager`。  
2. `DontDestroyOnLoad` 仅 `WorldManager`。  
3. `NetworkManager.Instance` 零匹配。

- [ ] **Step 2: Unity 编译验证**

操作：打开 Unity Editor 等待脚本编译完成。  
Expected: Console 无编译错误。

- [ ] **Step 3: Play Mode 生命周期验证**

手动验证：
1. 启动 Play，Hierarchy 仅 1 个 `WorldManager` 根对象（切换场景后仍保留）。  
2. 重进 Play 不出现重复 manager。  
3. UI 能正常显示、连接、创建/加入/离开流程可走通。  
4. 断线后 UI 回到登录面板。

- [ ] **Step 4: Commit（final）**

```bash
git add -A
git commit -m "refactor: world-root architecture with managed network and ui lifecycle"
```

