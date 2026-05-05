using System.Collections.Generic;
using Pathfinding.Data;
using UnityEngine;

namespace NavMeshDemo
{
    /// <summary>
    /// Renders the three path overlays (portal edges, midpoint polyline, funnel polyline)
    /// plus the start/goal markers. Coordinates are Pathfinding's XZ plane mapped to
    /// Unity's XY with a Z-depth offset per layer.
    /// </summary>
    public sealed class PathOverlayRenderer : MonoBehaviour
    {
        private static readonly Color _Portal = new Color32(0xFE, 0xB9, 0x4A, 0xFF);
        private static readonly Color _MidpointPath = new Color32(0xEE, 0x93, 0x8F, 0xFF);
        private static readonly Color _FunnelPath = new Color32(0xAA, 0xE0, 0x62, 0xFF);
        private static readonly Color _Endpoint = new Color32(0xFF, 0x63, 0x98, 0xFF);

        private const float _PortalZ = 0.2f;
        private const float _MidpointZ = 0.1f;
        private const float _FunnelZ = 0f;
        private const float _EndpointZ = -0.1f;
        private const float _PortalWidth = 0.12f;
        private const float _PathWidth = 0.08f;
        private const float _EndpointRadius = 0.35f;

        private NavMesh2D _Mesh;
        private GameObject _PortalRoot;
        private LineRenderer _MidpointLine;
        private LineRenderer _FunnelLine;
        private GameObject _StartMarker;
        private GameObject _GoalMarker;

        public void Bind(NavMesh2D navMesh)
        {
            _Mesh = navMesh;
            _PortalRoot = _MakeChild("Portals");
            _MidpointLine = _MakeLine("MidpointPath", _MidpointPath, _PathWidth, _MidpointZ);
            _FunnelLine = _MakeLine("FunnelPath", _FunnelPath, _PathWidth, _FunnelZ);
            _StartMarker = _MakeDisc("Start", _Endpoint, _EndpointRadius, _EndpointZ);
            _GoalMarker = _MakeDisc("Goal", _Endpoint, _EndpointRadius, _EndpointZ);
        }

        public void SetEndpoints(Vec2 start, Vec2 goal)
        {
            _StartMarker.transform.position = new Vector3(start.X, start.Z, _EndpointZ);
            _GoalMarker.transform.position = new Vector3(goal.X, goal.Z, _EndpointZ);
        }

        public void SetPortals(IReadOnlyList<int> corridor)
        {
            _ClearChildren(_PortalRoot);
            if (corridor == null || corridor.Count < 2)
                return;
            for (int i = 0; i < corridor.Count - 1; i++)
            {
                var (a, b) = _SharedEdge(corridor[i], corridor[i + 1]);
                _DrawPortal(a, b);
            }
        }

        public void SetMidpointPath(Vec2 start, IReadOnlyList<int> corridor, Vec2 goal)
        {
            if (corridor == null || corridor.Count < 1)
            {
                _MidpointLine.positionCount = 2;
                _MidpointLine.SetPosition(0, new Vector3(start.X, start.Z, _MidpointZ));
                _MidpointLine.SetPosition(1, new Vector3(goal.X, goal.Z, _MidpointZ));
                return;
            }
            var pts = new List<Vector3>(corridor.Count + 1);
            pts.Add(new Vector3(start.X, start.Z, _MidpointZ));
            for (int i = 0; i < corridor.Count - 1; i++)
            {
                var (a, b) = _SharedEdge(corridor[i], corridor[i + 1]);
                pts.Add(new Vector3((a.X + b.X) * 0.5f, (a.Z + b.Z) * 0.5f, _MidpointZ));
            }
            pts.Add(new Vector3(goal.X, goal.Z, _MidpointZ));
            _MidpointLine.positionCount = pts.Count;
            _MidpointLine.SetPositions(pts.ToArray());
        }

        public void SetFunnelPath(IReadOnlyList<Vec2> path)
        {
            if (path == null || path.Count == 0)
            {
                _FunnelLine.positionCount = 0;
                return;
            }
            var pts = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++)
                pts[i] = new Vector3(path[i].X, path[i].Z, _FunnelZ);
            _FunnelLine.positionCount = pts.Length;
            _FunnelLine.SetPositions(pts);
        }

        private (Vec2 A, Vec2 B) _SharedEdge(int curIdx, int nextIdx)
        {
            var tri = _Mesh.Triangles[curIdx];
            if (tri.N0 == nextIdx)
                return (_Mesh.Vertices[tri.V1], _Mesh.Vertices[tri.V2]);
            if (tri.N1 == nextIdx)
                return (_Mesh.Vertices[tri.V0], _Mesh.Vertices[tri.V2]);
            return (_Mesh.Vertices[tri.V0], _Mesh.Vertices[tri.V1]);
        }

        private void _DrawPortal(Vec2 a, Vec2 b)
        {
            var go = _MakeChild("Portal", _PortalRoot.transform);
            var lr = go.AddComponent<LineRenderer>();
            _ConfigLine(lr, _Portal, _PortalWidth);
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(a.X, a.Z, _PortalZ));
            lr.SetPosition(1, new Vector3(b.X, b.Z, _PortalZ));
        }

        private GameObject _MakeChild(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, worldPositionStays: false);
            return go;
        }

        private LineRenderer _MakeLine(string name, Color color, float width, float z)
        {
            var go = _MakeChild(name);
            go.transform.position = new Vector3(0f, 0f, z);
            var lr = go.AddComponent<LineRenderer>();
            _ConfigLine(lr, color, width);
            return lr;
        }

        private static void _ConfigLine(LineRenderer lr, Color color, float width)
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.positionCount = 0;
        }

        private GameObject _MakeDisc(string name, Color color, float radius, float z)
        {
            var go = _MakeChild(name);
            const int segments = 24;
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = ((i + 1) % segments) + 1;
            }
            var mesh = new Mesh { name = name + ".Mesh" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = color };
            go.transform.position = new Vector3(0f, 0f, z);
            return go;
        }

        private static void _ClearChildren(GameObject parent)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                Destroy(parent.transform.GetChild(i).gameObject);
        }
    }
}
