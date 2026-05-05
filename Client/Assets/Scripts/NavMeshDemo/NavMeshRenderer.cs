using System.Collections.Generic;
using Pathfinding.Data;
using UnityEngine;

namespace NavMeshDemo
{
    /// <summary>
    /// Builds three sibling meshes parented to its own GameObject:
    ///  - base fill (all triangles, dim grey)
    ///  - corridor fill (highlighted triangles from the latest A* result)
    ///  - wireframe (white lines around every triangle)
    /// </summary>
    public sealed class NavMeshRenderer : MonoBehaviour
    {
        private static readonly Color _BaseFill = new Color32(0x33, 0x33, 0x33, 0xFF);
        private static readonly Color _CorridorFill = new Color32(0xFF, 0x63, 0x98, 0x80);
        private static readonly Color _Wire = new Color32(0x55, 0x55, 0x55, 0xFF);

        private const float _BaseZ = 0.5f;
        private const float _CorridorZ = 0.4f;
        private const float _WireZ = 0.3f;

        private NavMesh2D _Mesh;
        private Mesh _BaseMesh;
        private Mesh _CorridorMesh;
        private Mesh _WireMesh;
        private MeshRenderer _CorridorRenderer;

        public void Bind(NavMesh2D navMesh)
        {
            _Mesh = navMesh;
            _BuildBaseMesh();
            _BuildWireMesh();
            _EnsureCorridorChild();
            SetCorridor(null);
        }

        public void SetCorridor(IReadOnlyList<int> corridor)
        {
            if (_Mesh == null)
                return;
            if (_CorridorMesh == null)
                _CorridorMesh = new Mesh { name = "NavMesh.Corridor" };
            _CorridorMesh.Clear();
            if (corridor == null || corridor.Count == 0)
            {
                _CorridorRenderer.enabled = false;
                return;
            }
            _CorridorRenderer.enabled = true;

            var verts = new Vector3[corridor.Count * 3];
            var tris = new int[corridor.Count * 3];
            for (int i = 0; i < corridor.Count; i++)
            {
                var t = _Mesh.Triangles[corridor[i]];
                Vec2 v0 = _Mesh.Vertices[t.V0];
                Vec2 v1 = _Mesh.Vertices[t.V1];
                Vec2 v2 = _Mesh.Vertices[t.V2];
                verts[i * 3 + 0] = new Vector3(v0.X, v0.Z, _CorridorZ);
                verts[i * 3 + 1] = new Vector3(v1.X, v1.Z, _CorridorZ);
                verts[i * 3 + 2] = new Vector3(v2.X, v2.Z, _CorridorZ);
                tris[i * 3 + 0] = i * 3 + 0;
                tris[i * 3 + 1] = i * 3 + 1;
                tris[i * 3 + 2] = i * 3 + 2;
            }
            _CorridorMesh.vertices = verts;
            _CorridorMesh.triangles = tris;
            _CorridorMesh.RecalculateBounds();
            _CorridorRenderer.GetComponent<MeshFilter>().sharedMesh = _CorridorMesh;
        }

        private void _BuildBaseMesh()
        {
            int triCount = _Mesh.Triangles.Count;
            var verts = new Vector3[triCount * 3];
            var tris = new int[triCount * 3];
            for (int i = 0; i < triCount; i++)
            {
                var t = _Mesh.Triangles[i];
                Vec2 v0 = _Mesh.Vertices[t.V0];
                Vec2 v1 = _Mesh.Vertices[t.V1];
                Vec2 v2 = _Mesh.Vertices[t.V2];
                verts[i * 3 + 0] = new Vector3(v0.X, v0.Z, _BaseZ);
                verts[i * 3 + 1] = new Vector3(v1.X, v1.Z, _BaseZ);
                verts[i * 3 + 2] = new Vector3(v2.X, v2.Z, _BaseZ);
                tris[i * 3 + 0] = i * 3 + 0;
                tris[i * 3 + 1] = i * 3 + 1;
                tris[i * 3 + 2] = i * 3 + 2;
            }
            _BaseMesh = new Mesh { name = "NavMesh.Base" };
            _BaseMesh.vertices = verts;
            _BaseMesh.triangles = tris;
            _BaseMesh.RecalculateBounds();

            var baseGo = _MakeChild("Base");
            _MakeMeshRenderer(baseGo, _BaseMesh, _BaseFill);
        }

        private void _BuildWireMesh()
        {
            int triCount = _Mesh.Triangles.Count;
            var verts = new Vector3[triCount * 3];
            var indices = new int[triCount * 6];
            for (int i = 0; i < triCount; i++)
            {
                var t = _Mesh.Triangles[i];
                Vec2 v0 = _Mesh.Vertices[t.V0];
                Vec2 v1 = _Mesh.Vertices[t.V1];
                Vec2 v2 = _Mesh.Vertices[t.V2];
                verts[i * 3 + 0] = new Vector3(v0.X, v0.Z, _WireZ);
                verts[i * 3 + 1] = new Vector3(v1.X, v1.Z, _WireZ);
                verts[i * 3 + 2] = new Vector3(v2.X, v2.Z, _WireZ);
                indices[i * 6 + 0] = i * 3 + 0;
                indices[i * 6 + 1] = i * 3 + 1;
                indices[i * 6 + 2] = i * 3 + 1;
                indices[i * 6 + 3] = i * 3 + 2;
                indices[i * 6 + 4] = i * 3 + 2;
                indices[i * 6 + 5] = i * 3 + 0;
            }
            _WireMesh = new Mesh { name = "NavMesh.Wire" };
            _WireMesh.vertices = verts;
            _WireMesh.SetIndices(indices, MeshTopology.Lines, 0);
            _WireMesh.RecalculateBounds();

            var wireGo = _MakeChild("Wire");
            _MakeMeshRenderer(wireGo, _WireMesh, _Wire);
        }

        private void _EnsureCorridorChild()
        {
            var go = _MakeChild("Corridor");
            _CorridorRenderer = _MakeMeshRenderer(go, null, _CorridorFill);
            _CorridorRenderer.enabled = false;
        }

        private GameObject _MakeChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            return go;
        }

        private MeshRenderer _MakeMeshRenderer(GameObject go, Mesh mesh, Color color)
        {
            var mf = go.AddComponent<MeshFilter>();
            if (mesh != null)
                mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { color = color };
            mr.sharedMaterial = mat;
            return mr;
        }
    }
}
