# AStarSolver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `Pathfinding.Triangulation.AStarSolver` — an A\* search over the triangle dual graph of `NavMesh2D`, returning a corridor of triangle indices (or `null` on failure).

**Architecture:** Static class with single `Solve(mesh, start, goal)` entry point. Uses centroid-to-centroid Euclidean cost and heuristic. Parallel arrays indexed by triangle id (`gScore`, `cameFrom`, `closed`). Private nested min-heap with lazy-decrease-key. Spec: [Client/Assets/Docs/superpowers/specs/2026-05-03-astar-solver-design.md](../specs/2026-05-03-astar-solver-design.md).

**Tech Stack:** C# `netstandard 2.1` (library) / `net8.0` (tests), xUnit 2.9, nullable reference types enabled.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Share/Pathfinding.Core/Triangulation/AStarSolver.cs` | CREATE | Public static `Solve` + private helpers + private `MinHeap` |
| `Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs` | CREATE | xUnit tests (5 facts + 2 helpers) |
| `Client/Assets/Docs/architecture/pathfinding-core.md` | MODIFY | Remove AStarSolver from "待实现模块" + add AStarSolver section |

No modifications to existing code files.

---

## Test Commands

All tests run from repo root:

```
dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~AStarSolverTests
```

Build only (no test discovery) — for skeleton verification:

```
dotnet build Share/Pathfinding.Core/Pathfinding.Core.csproj
```

---

## Task 1: Skeleton + out-of-mesh tests

Create the file with method signature and the two out-of-mesh early returns. Everything else throws `NotImplementedException`. Verifies the test project wiring before any algorithm code.

**Files:**
- Create: `Share/Pathfinding.Core/Triangulation/AStarSolver.cs`
- Create: `Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs`

- [ ] **Step 1.1: Create the test file with out-of-mesh facts**

Write: `Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs`

```csharp
using Pathfinding.Geometry;
using Pathfinding.Triangulation;
using Xunit;

namespace Pathfinding.Tests.Triangulation
{
    public class AStarSolverTests
    {
        private static readonly Polygon Square = new Polygon(new[]
        {
            new Vec2(0f, 0f),
            new Vec2(10f, 0f),
            new Vec2(10f, 10f),
            new Vec2(0f, 10f),
        });

        [Fact]
        public void Solve_StartOutsideMesh_ReturnsNull()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var path = AStarSolver.Solve(mesh, new Vec2(-5f, -5f), new Vec2(5f, 5f));
            Assert.Null(path);
        }

        [Fact]
        public void Solve_GoalOutsideMesh_ReturnsNull()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var path = AStarSolver.Solve(mesh, new Vec2(5f, 5f), new Vec2(15f, 15f));
            Assert.Null(path);
        }
    }
}
```

- [ ] **Step 1.2: Run the tests — expect compilation failure**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~AStarSolverTests`
Expected: Build error — `The name 'AStarSolver' does not exist in the current context`.

- [ ] **Step 1.3: Create the AStarSolver skeleton**

Write: `Share/Pathfinding.Core/Triangulation/AStarSolver.cs`

```csharp
using System;
using System.Collections.Generic;
using Pathfinding.Geometry;

namespace Pathfinding.Triangulation
{
    public static class AStarSolver
    {
        public static IReadOnlyList<int>? Solve(NavMesh2D mesh, Vec2 start, Vec2 goal)
        {
            int startTri = mesh.FindTriangle(start);
            int goalTri = mesh.FindTriangle(goal);
            if (startTri < 0 || goalTri < 0)
                return null;

            throw new NotImplementedException();
        }
    }
}
```

- [ ] **Step 1.4: Run the tests — expect both to PASS**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~AStarSolverTests`
Expected: 2 passed, 0 failed.

- [ ] **Step 1.5: Commit**

```
git add Share/Pathfinding.Core/Triangulation/AStarSolver.cs Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs
git commit -m "feat: AStarSolver skeleton with out-of-mesh fail-fast"
```

---

## Task 2: Same-triangle fast path

Start and goal inside the same triangle must return a single-element list without running A\*.

**Files:**
- Modify: `Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs`
- Modify: `Share/Pathfinding.Core/Triangulation/AStarSolver.cs`

- [ ] **Step 2.1: Add the same-triangle test**

Add this fact to `AStarSolverTests` (keep the other facts unchanged):

```csharp
        [Fact]
        public void Solve_SameTriangle_ReturnsSingleElement()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var p1 = new Vec2(1f, 1f);
            int tri1 = mesh.FindTriangle(p1);
            Assert.True(tri1 >= 0);
            // Centroid of a triangle is guaranteed to lie inside it,
            // so both points must map to the same triangle.
            Vec2 p2 = mesh.Triangles[tri1].Centroid(mesh.Vertices);

            var path = AStarSolver.Solve(mesh, p1, p2);

            Assert.NotNull(path);
            Assert.Single(path!);
            Assert.Equal(tri1, path![0]);
        }
```

- [ ] **Step 2.2: Run the test — expect FAIL (NotImplementedException)**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~Solve_SameTriangle_ReturnsSingleElement`
Expected: `System.NotImplementedException`.

- [ ] **Step 2.3: Add same-triangle early return**

Replace the body of `Solve` in `AStarSolver.cs`:

```csharp
        public static IReadOnlyList<int>? Solve(NavMesh2D mesh, Vec2 start, Vec2 goal)
        {
            int startTri = mesh.FindTriangle(start);
            int goalTri = mesh.FindTriangle(goal);
            if (startTri < 0 || goalTri < 0)
                return null;
            if (startTri == goalTri)
                return new[] { startTri };

            throw new NotImplementedException();
        }
```

- [ ] **Step 2.4: Run the test — expect PASS**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~AStarSolverTests`
Expected: 3 passed.

- [ ] **Step 2.5: Commit**

```
git add Share/Pathfinding.Core/Triangulation/AStarSolver.cs Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs
git commit -m "feat: AStarSolver same-triangle fast path"
```

---

## Task 3: Core A\* algorithm (MinHeap + main loop)

Implement the full algorithm. Test: corner-to-corner in a 10×10 square must return a connected path whose first/last elements match `FindTriangle(start)` / `FindTriangle(goal)`.

**Files:**
- Modify: `Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs`
- Modify: `Share/Pathfinding.Core/Triangulation/AStarSolver.cs`

- [ ] **Step 3.1: Add the corner-to-corner test + connected-path helper**

Add to `AStarSolverTests` (helper at bottom of class):

```csharp
        [Fact]
        public void Solve_RectangleCorners_ReturnsConnectedPath()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var start = new Vec2(1f, 1f);
            var goal = new Vec2(9f, 9f);

            var path = AStarSolver.Solve(mesh, start, goal);

            Assert.NotNull(path);
            Assert.True(path!.Count >= 1);
            Assert.Equal(mesh.FindTriangle(start), path[0]);
            Assert.Equal(mesh.FindTriangle(goal), path[path.Count - 1]);
            AssertPathConnected(mesh, path);
        }

        private static void AssertPathConnected(NavMesh2D mesh, IReadOnlyList<int> path)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                var t = mesh.Triangles[path[i]];
                int next = path[i + 1];
                Assert.True(
                    t.N0 == next || t.N1 == next || t.N2 == next,
                    $"Triangle {path[i]} is not a neighbor of {next}");
            }
        }
```

- [ ] **Step 3.2: Run the test — expect FAIL (NotImplementedException)**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~Solve_RectangleCorners_ReturnsConnectedPath`
Expected: `System.NotImplementedException`.

- [ ] **Step 3.3: Replace AStarSolver.cs with full implementation**

Overwrite `Share/Pathfinding.Core/Triangulation/AStarSolver.cs`:

```csharp
using System;
using System.Collections.Generic;
using Pathfinding.Geometry;

namespace Pathfinding.Triangulation
{
    public static class AStarSolver
    {
        public static IReadOnlyList<int>? Solve(NavMesh2D mesh, Vec2 start, Vec2 goal)
        {
            int startTri = mesh.FindTriangle(start);
            int goalTri = mesh.FindTriangle(goal);
            if (startTri < 0 || goalTri < 0)
                return null;
            if (startTri == goalTri)
                return new[] { startTri };

            int count = mesh.Triangles.Count;
            var centroids = _BuildCentroids(mesh);

            var gScore = new float[count];
            var cameFrom = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                gScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }
            gScore[startTri] = 0f;

            var open = new MinHeap(count);
            open.Push(startTri, centroids[startTri].DistanceTo(goal));

            while (open.TryPop(out int cur, out _))
            {
                if (closed[cur])
                    continue;
                if (cur == goalTri)
                    return _Reconstruct(cameFrom, startTri, goalTri);
                closed[cur] = true;

                var t = mesh.Triangles[cur];
                for (int slot = 0; slot < 3; slot++)
                {
                    int nb = _GetNeighbor(t, slot);
                    if (nb < 0 || closed[nb])
                        continue;
                    float tentativeG = gScore[cur] + centroids[cur].DistanceTo(centroids[nb]);
                    if (tentativeG < gScore[nb])
                    {
                        gScore[nb] = tentativeG;
                        cameFrom[nb] = cur;
                        open.Push(nb, tentativeG + centroids[nb].DistanceTo(goal));
                    }
                }
            }
            return null;
        }

        private static Vec2[] _BuildCentroids(NavMesh2D mesh)
        {
            int count = mesh.Triangles.Count;
            var centroids = new Vec2[count];
            for (int i = 0; i < count; i++)
                centroids[i] = mesh.Triangles[i].Centroid(mesh.Vertices);
            return centroids;
        }

        private static int _GetNeighbor(NavTriangle t, int slot)
        {
            switch (slot)
            {
                case 0: return t.N0;
                case 1: return t.N1;
                case 2: return t.N2;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static IReadOnlyList<int> _Reconstruct(int[] cameFrom, int startTri, int goalTri)
        {
            var reverse = new List<int>();
            int cur = goalTri;
            while (cur != -1)
            {
                reverse.Add(cur);
                if (cur == startTri)
                    break;
                cur = cameFrom[cur];
            }
            reverse.Reverse();
            return reverse;
        }

        private sealed class MinHeap
        {
            private (int Idx, float F)[] _Data;
            private int _Count;

            public MinHeap(int capacity)
            {
                _Data = new (int, float)[Math.Max(capacity, 8)];
                _Count = 0;
            }

            public void Push(int idx, float f)
            {
                if (_Count == _Data.Length)
                {
                    var grown = new (int, float)[_Data.Length * 2];
                    Array.Copy(_Data, grown, _Data.Length);
                    _Data = grown;
                }
                _Data[_Count] = (idx, f);
                _SiftUp(_Count);
                _Count++;
            }

            public bool TryPop(out int idx, out float f)
            {
                if (_Count == 0)
                {
                    idx = -1;
                    f = 0f;
                    return false;
                }
                idx = _Data[0].Idx;
                f = _Data[0].F;
                _Count--;
                if (_Count > 0)
                {
                    _Data[0] = _Data[_Count];
                    _SiftDown(0);
                }
                return true;
            }

            private void _SiftUp(int i)
            {
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_Data[i].F < _Data[parent].F)
                    {
                        var tmp = _Data[i];
                        _Data[i] = _Data[parent];
                        _Data[parent] = tmp;
                        i = parent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private void _SiftDown(int i)
            {
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = i * 2 + 2;
                    int smallest = i;
                    if (left < _Count && _Data[left].F < _Data[smallest].F)
                        smallest = left;
                    if (right < _Count && _Data[right].F < _Data[smallest].F)
                        smallest = right;
                    if (smallest == i)
                        break;
                    var tmp = _Data[i];
                    _Data[i] = _Data[smallest];
                    _Data[smallest] = tmp;
                    i = smallest;
                }
            }
        }
    }
}
```

- [ ] **Step 3.4: Run all AStarSolver tests — expect 4 pass**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~AStarSolverTests`
Expected: 4 passed, 0 failed.

- [ ] **Step 3.5: Run full test suite to confirm no regressions**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj`
Expected: All existing tests still pass.

- [ ] **Step 3.6: Commit**

```
git add Share/Pathfinding.Core/Triangulation/AStarSolver.cs Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs
git commit -m "feat: AStarSolver A* main loop with min-heap open list"
```

---

## Task 4: Obstacle-avoidance test

A test for path quality: with a central 4×4 hole in a 10×10 square, a path from (1,5) to (9,5) must route around the hole. This should pass with the Task 3 implementation unchanged — it verifies algorithmic correctness on a harder case.

**Files:**
- Modify: `Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs`

- [ ] **Step 4.1: Add the obstacle polygon and test**

Add to `AStarSolverTests` (polygon field near the top; test near the other facts):

```csharp
        private static readonly Polygon InnerSquare = new Polygon(new[]
        {
            new Vec2(3f, 3f),
            new Vec2(7f, 3f),
            new Vec2(7f, 7f),
            new Vec2(3f, 7f),
        });

        [Fact]
        public void Solve_AroundObstacle_PathAvoidsHole()
        {
            var mesh = NavMeshBuilder.Build(Square, new[] { InnerSquare });
            var start = new Vec2(1f, 5f);
            var goal = new Vec2(9f, 5f);

            var path = AStarSolver.Solve(mesh, start, goal);

            Assert.NotNull(path);
            AssertPathConnected(mesh, path!);
            foreach (int triIdx in path!)
            {
                var centroid = mesh.Triangles[triIdx].Centroid(mesh.Vertices);
                bool insideHole = centroid.X > 3f && centroid.X < 7f
                               && centroid.Z > 3f && centroid.Z < 7f;
                Assert.False(insideHole,
                    $"Triangle {triIdx} centroid {centroid} is inside the hole");
            }
        }
```

- [ ] **Step 4.2: Run the test — expect PASS**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~Solve_AroundObstacle_PathAvoidsHole`
Expected: 1 passed.

- [ ] **Step 4.3: Run all AStarSolver tests**

Run: `dotnet test Share/Pathfinding.Core.Tests/Pathfinding.Core.Tests.csproj --filter FullyQualifiedName~AStarSolverTests`
Expected: 5 passed, 0 failed.

- [ ] **Step 4.4: Commit**

```
git add Share/Pathfinding.Core.Tests/Triangulation/AStarSolverTests.cs
git commit -m "test: AStarSolver obstacle-avoidance path check"
```

---

## Task 5: Architecture documentation update

Update the public architecture doc to reflect that AStarSolver now exists.

**Files:**
- Modify: `Client/Assets/Docs/architecture/pathfinding-core.md`

- [ ] **Step 5.1: Remove AStarSolver row from the "待实现模块" table**

Open `Client/Assets/Docs/architecture/pathfinding-core.md`, find the section:

```
## 待实现模块

| 模块 | 位置 | 说明 |
|---|---|---|
| `AStarSolver` | `Triangulation/` | A\* 在三角形对偶图上寻路，输出三角走廊 |
| `FunnelAlgorithm` | `Algorithm/` | 漏斗算法，将三角走廊 + Portal 序列转为最短折线路径 |
| `LosChecker` | `Algorithm/` | 可选后处理，合并共线路段 |
```

Delete the `AStarSolver` row only. Final form:

```
## 待实现模块

| 模块 | 位置 | 说明 |
|---|---|---|
| `FunnelAlgorithm` | `Algorithm/` | 漏斗算法，将三角走廊 + Portal 序列转为最短折线路径 |
| `LosChecker` | `Algorithm/` | 可选后处理，合并共线路段 |
```

- [ ] **Step 5.2: Add AStarSolver section after NavMeshBuilder**

In the "核心类型" region, locate the end of the `NavMeshBuilder` subsection (just before `### NavMeshAsset — JSON 序列化`). Insert the following subsection between them (preserve surrounding `---` separators):

```
### `AStarSolver` — 三角形对偶图 A\*

\`\`\`csharp
public static class AStarSolver
{
    public static IReadOnlyList<int>? Solve(NavMesh2D mesh, Vec2 start, Vec2 goal);
}
\`\`\`

**语义：**

| 情况 | 返回值 |
|---|---|
| `start` 或 `goal` 不在网格内 | `null` |
| `start` 与 `goal` 落在同一三角形 | 单元素列表 `[triIdx]` |
| 两端连通 | 从起点三角形到终点三角形的索引序列，相邻元素互为邻居 |
| 两端不连通 | `null` |

**代价与启发：** 三角形质心之间的欧氏距离。Funnel 阶段会重算真实最短距离，A\* 的质心估值偏差在此处被吸收。

**实现要点：** 并行数组（`float[] gScore`、`int[] cameFrom`、`bool[] closed`）按三角形索引访问；内部私有 `MinHeap` 使用惰性失效策略处理 decrease-key。
```

(Remove the backslash-escaped backticks when writing the actual file — they are escaped here to avoid nesting inside this plan's code fence.)

- [ ] **Step 5.3: Verify the change renders**

Run: `dotnet build Share/Pathfinding.Core/Pathfinding.Core.csproj`
Expected: Build succeeds (sanity check that nothing broke).

- [ ] **Step 5.4: Commit**

```
git add Client/Assets/Docs/architecture/pathfinding-core.md
git commit -m "docs: document AStarSolver in pathfinding-core architecture"
```

---

## Self-Review Checklist (for reviewer, not part of execution)

- Spec §API: covered by Task 1 + 3 (signature + `null` semantics)
- Spec §Algorithm: covered by Task 3 (centroids, parallel arrays, lazy min-heap)
- Spec §Tests (5 cases): Task 1 covers 2 out-of-mesh; Task 2 covers same-triangle; Task 3 covers rectangle corners; Task 4 covers obstacle avoidance. All 5 cases present.
- Spec §Documentation updates: Task 5
- No placeholders, no "TBD", all code blocks complete
- Naming consistency: `_BuildCentroids`, `_GetNeighbor`, `_Reconstruct`, `_SiftUp`, `_SiftDown` all private-method underscore-prefix per C# coding standard; `_Data`, `_Count` private fields follow `_PascalCase`; public `Solve` is PascalCase; helper `AssertPathConnected` in tests is PascalCase (public in test class context)
