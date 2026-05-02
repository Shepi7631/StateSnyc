# Pathfinding.Core 架构文档

## 概述

`Pathfinding.Core` 是一个 **netstandard 2.1 共享库**，同时被 Unity 客户端和 C# 服务端引用，提供统一的 2D（XZ 平面）导航网格构建能力。两端使用同一份寻路代码，保证客户端预测与服务端权威路径的高度一致性。

```
StateSnyc/
└── Share/
    ├── Proto/                   (Protobuf 定义)
    └── Pathfinding.Core/        (本库)
```

---

## 依赖

| 包 | 版本 | 用途 |
|---|---|---|
| `Unofficial.Triangle.NET` | 0.0.1 | Constrained Delaunay Triangulation (CDT) |
| `System.Text.Json` | 8.0.0 | NavMeshAsset JSON 序列化 |

> `Triangle.NET` 底层基于 Recast/Detour 相同的算法理念，在 XZ 平面执行 CDT，生成覆盖可行走区域的三角网格。

---

## 目录结构

```
Pathfinding.Core/
├── Geometry/
│   ├── Vec2.cs          — 轻量 2D 点 (float X, float Z)，无 UnityEngine 依赖
│   ├── Polygon.cs       — 有序顶点列表，表示边界或障碍物轮廓
│   ├── NavTriangle.cs   — 三角形：3 个顶点索引 + 3 个邻居三角形索引
│   └── Portal.cs        — 两个相邻三角形的共享边（漏斗入口）
├── Triangulation/
│   ├── NavMeshBuilder.cs — Triangle.NET CDT 封装，输入多边形 → NavMesh2D
│   └── NavMesh2D.cs      — 运行时网格：顶点数组 + 三角形数组 + FindTriangle()
└── Data/
    └── NavMeshAsset.cs   — JSON 序列化/反序列化 + Build() 入口
```

---

## 核心类型

### `Vec2` — 轻量 2D 点

```csharp
public readonly struct Vec2 : IEquatable<Vec2>
{
    public readonly float X;
    public readonly float Z;

    public float DistanceTo(Vec2 other);
    public static Vec2 operator +(Vec2 a, Vec2 b);
    public static Vec2 operator -(Vec2 a, Vec2 b);
    public static Vec2 operator *(Vec2 v, float s);
    public static bool operator ==(Vec2 a, Vec2 b);
    public static bool operator !=(Vec2 a, Vec2 b);
}
```

所有寻路坐标使用 XZ 平面。Unity 侧转换：

```csharp
Vector3 ToUnity(Vec2 v, float y = 0f) => new Vector3(v.X, y, v.Z);
Vec2 FromUnity(Vector3 v) => new Vec2(v.x, v.z);
```

---

### `Polygon` — 多边形轮廓

```csharp
public sealed class Polygon
{
    public IReadOnlyList<Vec2> Vertices { get; }
    public Polygon(IEnumerable<Vec2> vertices); // 至少 3 个顶点
}
```

用于表示可行走区域边界（`boundary`）或障碍物轮廓（`obstacles`）。顶点建议按逆时针顺序排列。

---

### `NavTriangle` — 导航三角形

```csharp
public sealed class NavTriangle
{
    public int Index { get; }   // 在 NavMesh2D.Triangles 中的下标
    public int V0, V1, V2;      // 顶点在 NavMesh2D.Vertices 中的下标
    public int N0, N1, N2;      // 邻居三角形下标，-1 表示边界边

    // N0 对面 V0（共享边 V1-V2）
    // N1 对面 V1（共享边 V0-V2）
    // N2 对面 V2（共享边 V0-V1）

    public Vec2 Centroid(IReadOnlyList<Vec2> vertices);
    public Portal GetPortal(int neighborSlot, IReadOnlyList<Vec2> vertices);
}
```

邻居索引是 A\* 对偶图寻路和 Funnel Algorithm 的基础数据结构。

---

### `Portal` — 三角形共享边

```csharp
public readonly struct Portal
{
    public readonly Vec2 Left;
    public readonly Vec2 Right;
}
```

Funnel Algorithm 使用 Portal 序列将三角走廊转为最短折线路径（待实现）。

---

### `NavMesh2D` — 运行时网格

```csharp
public sealed class NavMesh2D
{
    public IReadOnlyList<Vec2> Vertices { get; }
    public IReadOnlyList<NavTriangle> Triangles { get; }

    // 返回包含 point 的三角形下标，未找到返回 -1
    public int FindTriangle(Vec2 point);
}
```

`FindTriangle` 使用叉积符号判断点是否在三角形内（O(n)，适合小型地图）。

---

### `NavMeshBuilder` — CDT 封装

```csharp
public static class NavMeshBuilder
{
    public static NavMesh2D Build(Polygon boundary, IEnumerable<Polygon>? obstacles = null);
}
```

**内部流程：**

```
Polygon (boundary + obstacles)
  → Triangle.NET Constrained Delaunay Triangulation
  → 顶点 ID→下标映射
  → 三角形 ID→下标映射（Triangle.NET ID 从 1 开始）
  → 邻居索引通过 GetNeighborID() 转换（返回 -1 表示边界）
  → NavMesh2D
```

**注意：** Triangle.NET 顶点坐标使用 `.X` 和 `.Y`（不是 `.Z`）。在 `Convert` 内部，`tVerts[i].Y` 对应我们的 `Vec2.Z`。

---

### `NavMeshAsset` — JSON 序列化

```csharp
public sealed class NavMeshAsset
{
    public float[][] Boundary { get; set; }       // [[x,z], ...]
    public float[][][] Obstacles { get; set; }    // [[[x,z], ...], ...]

    public static NavMeshAsset FromPolygons(Polygon boundary, IEnumerable<Polygon>? obstacles = null);
    public Polygon GetBoundary();
    public IReadOnlyList<Polygon> GetObstacles();
    public NavMesh2D Build();
    public string ToJson();
    public static NavMeshAsset Load(string json);
}
```

**JSON 格式：**

```json
{
  "boundary": [[0,0],[10,0],[10,10],[0,10]],
  "obstacles": [
    [[3,3],[7,3],[7,7],[3,7]]
  ]
}
```

---

## 使用流程

### 离线（编辑器 / 工具）：构建并保存网格

```csharp
var boundary = new Polygon(new[] {
    new Vec2(0, 0), new Vec2(50, 0),
    new Vec2(50, 50), new Vec2(0, 50)
});
var obstacle = new Polygon(new[] {
    new Vec2(20, 20), new Vec2(30, 20),
    new Vec2(30, 30), new Vec2(20, 30)
});

var asset = NavMeshAsset.FromPolygons(boundary, new[] { obstacle });
File.WriteAllText("navmesh.json", asset.ToJson());
```

### 运行时：加载并查询

```csharp
// 加载（客户端和服务端共用同一份 JSON）
var asset = NavMeshAsset.Load(File.ReadAllText("navmesh.json"));
NavMesh2D mesh = asset.Build();

// 查询点所在三角形
int triIdx = mesh.FindTriangle(new Vec2(5f, 5f));
if (triIdx >= 0)
{
    var tri = mesh.Triangles[triIdx];
    Vec2 centroid = tri.Centroid(mesh.Vertices);
}
```

---

## 架构约束

| 约束 | 说明 |
|---|---|
| XZ 平面 | 所有坐标仅使用 X 和 Z，Y 由调用方管理（通常固定为 0） |
| 静态障碍物 | CDT 离线烘焙，运行时不重新剖分；动态障碍物用 Steering 处理 |
| 无 Unity 依赖 | 库可独立在服务端 .NET 运行，Unity 侧自行做 Vec2↔Vector3 转换 |
| netstandard 2.1 | Unity 2021+ 和 .NET 5+ 服务端均可直接引用 |

---

## 待实现模块

| 模块 | 位置 | 说明 |
|---|---|---|
| `AStarSolver` | `Triangulation/` | A\* 在三角形对偶图上寻路，输出三角走廊 |
| `FunnelAlgorithm` | `Algorithm/` | 漏斗算法，将三角走廊 + Portal 序列转为最短折线路径 |
| `LosChecker` | `Algorithm/` | 可选后处理，合并共线路段 |
