# AStarSolver 设计

**日期：** 2026-05-03
**模块：** `Pathfinding.Core/Triangulation/AStarSolver`
**目标读者：** Pathfinding.Core 维护者、客户端/服务端寻路调用方

---

## 背景

`Pathfinding.Core` 已完成 `NavMesh2D` 构建与资产化。架构文档（`Client/Assets/Docs/architecture/pathfinding-core.md`）将 `AStarSolver` 标记为下一个待实现模块，职责为"在三角形对偶图上寻路，输出三角走廊"。

本 spec 只覆盖 A\*；Funnel（三角走廊 → 最短折线）与 LoS 合并作为独立模块后续实现。

---

## 约束

- 目标框架 `netstandard 2.1`，`LangVersion 9.0`，`Nullable enable`
- 不引入新的 NuGet 依赖
- 客户端/服务端同代码复用，确定性由 `float` 精度承担（与既有模块一致）
- 不修改 `NavMesh2D` / `NavTriangle` 的既有字段与契约

---

## 公共 API

```csharp
namespace Pathfinding.Triangulation
{
    public static class AStarSolver
    {
        /// <summary>
        /// 在三角形对偶图上寻路，返回三角形索引走廊。
        /// start 或 goal 不在网格内、或两端点不连通时返回 null。
        /// start 与 goal 落在同一三角形时返回单元素列表。
        /// </summary>
        public static IReadOnlyList<int>? Solve(NavMesh2D mesh, Vec2 start, Vec2 goal);
    }
}
```

- 静态类，与 `NavMeshBuilder` 风格一致
- 失败显式返回 `null`（不抛异常、不返回空列表，避免与"起点即终点"的单元素结果混淆）
- 不提供 `out` 参数、不提供 snap 重载 —— YAGNI

---

## 算法

标准 A\* on 对偶图，节点 = 三角形索引。

### 代价与启发

- `centroids[i]` = `Triangles[i].Centroid(Vertices)`，构造时一次性预计算
- `g(n → m) = centroids[n].DistanceTo(centroids[m])`
- `h(n) = centroids[n].DistanceTo(goal)`（欧氏距离，admissible & consistent）

选型理由：实现最简、易测试；后续 Funnel 会重算真实最短距离，A\* 阶段的质心估值偏差可被吸收。

### 数据结构

按三角形数量开并行数组（稠密整数 id，天然适配数组）：

| 字段 | 类型 | 初值 |
|---|---|---|
| `gScore` | `float[]` | `float.PositiveInfinity` |
| `cameFrom` | `int[]` | `-1` |
| `closed` | `bool[]` | `false` |

Open list：私有嵌套 `MinHeap`，元素 `(int idx, float f)`，二叉堆实现。

### decrease-key 策略

**惰性失效**：发现更优 g 时再次 `Push`，`Pop` 出来后若 `closed[idx] == true` 或该 entry 的 `f` 大于当前 `gScore[idx] + h(idx)`（浮点比较允许相等），则丢弃继续 pop。

理由：实现简单；堆规模最坏为 `O(E)`，对小型地图影响可忽略。

### 主循环伪码

```
startTri = mesh.FindTriangle(start)
goalTri  = mesh.FindTriangle(goal)
if startTri < 0 || goalTri < 0: return null
if startTri == goalTri: return [startTri]

centroids = precompute()
init gScore, cameFrom, closed
gScore[startTri] = 0
heap.Push(startTri, h(startTri))

while heap not empty:
    (cur, f) = heap.Pop()
    if closed[cur]: continue
    if cur == goalTri: return Reconstruct(cameFrom, cur)
    closed[cur] = true

    for each slot in {0,1,2}:
        nb = Triangles[cur].N(slot)
        if nb < 0 || closed[nb]: continue
        tentativeG = gScore[cur] + centroids[cur].DistanceTo(centroids[nb])
        if tentativeG < gScore[nb]:
            gScore[nb] = tentativeG
            cameFrom[nb] = cur
            heap.Push(nb, tentativeG + centroids[nb].DistanceTo(goal))

return null
```

`Reconstruct`：从 `goalTri` 逆向追 `cameFrom` 收集到 `startTri`，反转后返回 `IReadOnlyList<int>`。

---

## 文件清单

```
Share/Pathfinding.Core/Triangulation/AStarSolver.cs        (新增)
Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs (新增)
Client/Assets/Docs/architecture/pathfinding-core.md        (更新)
```

`AStarSolver.cs` 内部结构：

- `public static class AStarSolver`
  - `public static IReadOnlyList<int>? Solve(...)`
  - `private static Vec2[] BuildCentroids(NavMesh2D mesh)`
  - `private static int GetNeighbor(NavTriangle t, int slot)`（slot 0/1/2 → N0/N1/N2）
  - `private static IReadOnlyList<int> Reconstruct(int[] cameFrom, int goalTri)`
  - `private sealed class MinHeap`（仅本类使用，不对外暴露）

---

## 测试（xUnit，`Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs`）

| 用例 | 输入 | 断言 |
|---|---|---|
| 同一三角形 | 方块内两个落在同一 tri 的点 | 返回长度 1 |
| 简单矩形对角 | 10×10 方块，(1,1) → (9,9) | 非空；首 tri 包含起点；末 tri 包含终点；路径连续 |
| 起点网格外 | (-5,-5) → (5,5) | `null` |
| 终点网格外 | (5,5) → (15,15) | `null` |
| 绕过障碍 | 10×10 方块 + 中心 4×4 障碍，(1,5) → (9,5) | 非空；路径每个三角形质心不在障碍内 |

**辅助断言** `AssertPathIsConnected(mesh, path)`：对任意相邻 `path[i], path[i+1]`，`Triangles[path[i]]` 的 `N0/N1/N2` 中至少一个等于 `path[i+1]`。

不单独测试堆：端到端用例覆盖堆的 pop 顺序正确性。

---

## 文档更新

`Client/Assets/Docs/architecture/pathfinding-core.md`：

1. "待实现模块"表移除 `AStarSolver` 行
2. 在 `NavMeshBuilder` 小节之后插入 `AStarSolver` 小节：
   - 公共 API 签名
   - 代价/启发选择说明
   - 失败语义（`null` vs 单元素）
   - 与 Funnel 的分工

---

## 非目标

- Funnel、LoS 后处理（独立 spec）
- 动态障碍物 / 网格重建
- 路径平滑
- 起/终点 snap 到最近三角形
- 堆的独立单元测试
- 性能基准
