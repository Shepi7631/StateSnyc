using System.Collections.Generic;
using System.IO;
using System.Text;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NavMeshDemo
{
    /// <summary>
    /// Entry point for the NavMesh visualization scene. Attach to any empty
    /// GameObject; the controller creates its camera, canvas, renderers, and
    /// loads StreamingAssets/navmesh.json on Awake.
    /// </summary>
    public sealed class NavMeshDemoController : MonoBehaviour
    {
        [SerializeField]
        private string _JsonFileName = "navmesh.json";

        private static readonly Vec2 _InitialStart = new Vec2(1f, 1f);
        private static readonly Vec2 _InitialGoal = new Vec2(19f, 19f);

        private NavMesh2D _Mesh;
        private NavMeshRenderer _MeshRenderer;
        private PathOverlayRenderer _Overlay;
        private Camera _Camera;
        private Vec2 _Start;
        private Vec2 _Goal;
        private IReadOnlyList<Vec2> _LastPath;

        private void Awake()
        {
            _LoadMesh();
            _SetupCamera();
            _SetupRenderers();
            _SetupLegend();

            _Start = _InitialStart;
            _Goal = _InitialGoal;
            _Recompute();
        }

        private void Update()
        {
            if (_Mesh == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_TryPickWalkable(out Vec2 p))
                {
                    _Start = p;
                    _Recompute();
                }
            }
            else if (Input.GetMouseButton(0))
            {
                if (_TryPickWalkable(out Vec2 p) && !p.Equals(_Goal))
                {
                    _Goal = p;
                    _Recompute();
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                _LogPath();
            }
        }

        private bool _TryPickWalkable(out Vec2 p)
        {
            Vector3 world = _Camera.ScreenToWorldPoint(Input.mousePosition);
            var candidate = new Vec2(world.x, world.y);
            if (_Mesh.FindTriangle(candidate) < 0)
            {
                p = default;
                return false;
            }
            p = candidate;
            return true;
        }

        private void _Recompute()
        {
            var corridor = AStarSolver.Solve(_Mesh, _Start, _Goal);
            _MeshRenderer.SetCorridor(corridor);
            _Overlay.SetEndpoints(_Start, _Goal);
            _Overlay.SetPortals(corridor);
            _Overlay.SetMidpointPath(_Start, corridor, _Goal);
            if (corridor != null)
            {
                var path = FunnelSolver.Solve(_Mesh, corridor, _Start, _Goal);
                _Overlay.SetFunnelPath(path);
                _LastPath = path;
            }
            else
            {
                _Overlay.SetFunnelPath(null);
                _LastPath = null;
            }
        }

        private void _LogPath()
        {
            if (_LastPath == null || _LastPath.Count == 0)
            {
                Debug.Log("[NavMeshDemo] No path available.");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("[NavMeshDemo] Funnel path (");
            sb.Append(_LastPath.Count);
            sb.Append(" points):\n");
            for (int i = 0; i < _LastPath.Count; i++)
            {
                Vec2 p = _LastPath[i];
                sb.AppendFormat("  [{0}] ({1:F3}, {2:F3})\n", i, p.X, p.Z);
            }
            Debug.Log(sb.ToString());
        }

        private void _LoadMesh()
        {
            string path = Path.Combine(Application.streamingAssetsPath, _JsonFileName);
            string json = File.ReadAllText(path);
            var (boundary, obstacles) = NavMeshJsonLoader.Load(json);
            _Mesh = NavMeshBuilder.Build(boundary, obstacles);
        }

        private void _SetupCamera()
        {
            var existing = Camera.main;
            if (existing != null)
            {
                _Camera = existing;
            }
            else
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _Camera = go.AddComponent<Camera>();
            }
            _Camera.orthographic = true;
            _Camera.clearFlags = CameraClearFlags.SolidColor;
            _Camera.backgroundColor = new Color32(0x11, 0x11, 0x11, 0xFF);
            _Camera.nearClipPlane = -1f;
            _Camera.farClipPlane = 10f;

            // Fit mesh in view. Boundary is the first polygon in the asset; use its AABB.
            Bounds b = _MeshBounds();
            _Camera.transform.position = new Vector3(b.center.x, b.center.y, -5f);
            float margin = 1.5f;
            float aspect = _Camera.aspect > 0f ? _Camera.aspect : 16f / 9f;
            _Camera.orthographicSize = Mathf.Max(
                b.extents.y + margin,
                (b.extents.x + margin) / aspect);
        }

        private Bounds _MeshBounds()
        {
            float xmin = float.PositiveInfinity, xmax = float.NegativeInfinity;
            float zmin = float.PositiveInfinity, zmax = float.NegativeInfinity;
            foreach (var v in _Mesh.Vertices)
            {
                if (v.X < xmin) xmin = v.X;
                if (v.X > xmax) xmax = v.X;
                if (v.Z < zmin) zmin = v.Z;
                if (v.Z > zmax) zmax = v.Z;
            }
            return new Bounds(
                new Vector3((xmin + xmax) * 0.5f, (zmin + zmax) * 0.5f, 0f),
                new Vector3(xmax - xmin, zmax - zmin, 0f));
        }

        private void _SetupRenderers()
        {
            var meshGo = new GameObject("NavMeshRenderer");
            meshGo.transform.SetParent(transform, worldPositionStays: false);
            _MeshRenderer = meshGo.AddComponent<NavMeshRenderer>();
            _MeshRenderer.Bind(_Mesh);

            var overlayGo = new GameObject("PathOverlayRenderer");
            overlayGo.transform.SetParent(transform, worldPositionStays: false);
            _Overlay = overlayGo.AddComponent<PathOverlayRenderer>();
            _Overlay.Bind(_Mesh);
        }

        private void _SetupLegend()
        {
            var canvasGo = new GameObject("Legend");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            _MakeLabel(canvasGo.transform, "Tips", "点击设置起点 · 按住左键拖动设置终点", new Vector2(16f, -16f), TextAnchor.UpperLeft);
            _MakeLabel(canvasGo.transform, "Legend",
                "<color=#ff6398>■</color> 起终点\n" +
                "<color=#ff6398>■</color> 网格路径\n" +
                "<color=#ee938f>■</color> 边中点路径\n" +
                "<color=#feb94a>■</color> 公共边\n" +
                "<color=#aae062>■</color> 平滑路径",
                new Vector2(-16f, -16f),
                TextAnchor.UpperRight);
        }

        private static void _MakeLabel(Transform parent, string name, string text, Vector2 anchoredPos, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = go.AddComponent<RectTransform>();
            if (anchor == TextAnchor.UpperLeft)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
            }
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(320f, 160f);
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = _GetDefaultFont();
            txt.fontSize = 16;
            txt.color = new Color(0.87f, 0.87f, 0.87f);
            txt.alignment = anchor;
            txt.supportRichText = true;
        }

        private static Font _CachedFont;

        private static Font _GetDefaultFont()
        {
            if (_CachedFont == null)
                _CachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return _CachedFont;
        }
    }
}
