# NavMesh Demo (Unity Runtime Visualization)

**Date:** 2026-05-03
**Goal:** Play-mode demo that visualizes A\* + Funnel over a NavMesh2D loaded from JSON.
**Reference:** [SLI97/2d-navmesh](https://github.com/SLI97/2d-navmesh) — HTML/Canvas demo; interaction and legend mirrored, implementation language/platform differs.

---

## Scope

- One new scene `Client/Assets/Scenes/NavMeshDemo.unity`
- Source-copy of `Share/Pathfinding.Core/` into `Client/Assets/Pathfinding.Core/` as a Unity asmdef (avoids `System.Text.Json` dependency; `NavMeshAsset.cs` deliberately excluded)
- `Triangle.dll` (Unofficial.Triangle.NET 0.0.1) copied into `Client/Assets/Plugins/` for CDT
- MonoBehaviours under `Client/Assets/Scripts/NavMeshDemo/`
- Demo map under `Client/Assets/StreamingAssets/navmesh.json`
- **No changes to `Share/Pathfinding.Core/`**

## Integration approach

| Concern | Decision |
|---|---|
| Pathfinding.Core → Unity | Source-copy .cs files (asmdef compiles in Unity) |
| CDT dependency | Ship `Triangle.dll` in `Plugins/` |
| JSON loading | Hand-rolled tiny parser in Unity (no `NavMeshAsset`, no `System.Text.Json`, no Newtonsoft) |
| Sync between Share and Unity copy | Manual; Pathfinding.Core is stable |

## Files

```
Client/Assets/
├── Plugins/
│   └── Triangle.dll                           NEW
├── Pathfinding.Core/                          NEW
│   ├── Pathfinding.Core.asmdef                NEW
│   ├── Data/ {Vec2,Polygon,NavTriangle,Portal,NavMesh2D}.cs   (copy from Share/; no NavMeshAsset.cs)
│   └── Algorithm/ {NavMeshBuilder,AStarSolver,FunnelSolver}.cs (copy from Share/)
├── Scenes/
│   └── NavMeshDemo.unity                      NEW
├── Scripts/NavMeshDemo/
│   ├── NavMeshDemoController.cs               NEW  main driver: load, input, recompute
│   ├── NavMeshRenderer.cs                     NEW  base mesh + wireframe + corridor highlight
│   ├── PathOverlayRenderer.cs                 NEW  portals / midpoint path / funnel path lines
│   └── NavMeshJsonLoader.cs                   NEW  parse the 2-field JSON into Pathfinding.Data.Polygon
└── StreamingAssets/
    └── navmesh.json                           NEW  20×20 boundary with 2 obstacles
```

## Interaction

Matches the reference legend (Chinese labels preserved):

- **点击** (left-click) on any walkable point → sets start
- **按住左键拖动** (drag) → sets goal, path recomputes every mouse-move
- Click/drag on non-walkable (outside mesh, or inside obstacle) → ignored, previous value retained
- Initial start/goal are hard-coded; first path is visible before any input

## Rendering

2D orthographic camera looking down the Z axis. Pathfinding's `Vec2(X, Z)` maps to Unity `Vector3(X, Z, depthZ)` (Pathfinding Z → Unity world Y).

| Legend | Color | Unity impl |
|---|---|---|
| Base fill (全 mesh) | `#333333` | Single static Mesh, unlit |
| 网格路径 (corridor) | `rgba(255,99,152,0.5)` | Dynamic Mesh rebuilt per input change |
| Wireframe | `#555555` | `GL.LINES` in `OnRenderObject` or LineRenderer |
| 公共边 (portal edges) | `#feb94a` | LineRenderer |
| 边中点路径 | `#ee938f` | LineRenderer |
| 平滑路径 (funnel) | `#aae062` | LineRenderer |
| 起终点 | `#ff6398` | Small disc (Mesh or Sprite) |

Depth sorting via Z offsets. Background `#111111`.

Legend text + tips in a UGUI Canvas corner (not part of rendering layer).

## Self-bootstrapping

`NavMeshDemoController` creates its Camera, Canvas, and child GameObjects programmatically in `Awake()`. Scene file contains only:

- `Main Camera` (or auto-created by controller)
- One empty GameObject `Demo` with `NavMeshDemoController` attached

This keeps the scene file minimal and robust to Unity version differences.

## Demo map (`navmesh.json`)

```json
{
  "boundary": [[0,0],[20,0],[20,20],[0,20]],
  "obstacles": [
    [[5,6],[11,6],[11,12],[5,12]],
    [[13,3],[17,3],[17,9],[13,9]]
  ]
}
```

Two rectangular obstacles leaving walkable corridors on all sides. Start/goal on opposite sides force the A\* corridor to detour around at least one obstacle.

## Non-goals

- Dynamic obstacles / in-game editing
- Performance benchmarks
- NavMeshAsset round-trip in Unity (out of scope; demo reads raw JSON)
- Unit tests for Unity MonoBehaviours (Unity Play-mode testing is heavy for this demo)
