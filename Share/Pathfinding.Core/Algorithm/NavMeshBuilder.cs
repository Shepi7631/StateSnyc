using System.Collections.Generic;
using System.Linq;
using Pathfinding.Data;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using NavPolygon = Pathfinding.Data.Polygon;
using NavVec2 = Pathfinding.Data.Vec2;
using TNetPolygon = TriangleNet.Geometry.Polygon;
using TNetMesh = TriangleNet.Mesh;

namespace Pathfinding.Algorithm
{
    public static class NavMeshBuilder
    {
        public static NavMesh2D Build(NavPolygon boundary, IEnumerable<NavPolygon>? obstacles = null)
        {
            var input = new TNetPolygon();

            var boundaryVerts = boundary.Vertices
                .Select(v => new Vertex(v.X, v.Z))
                .ToList();
            input.Add(new Contour(boundaryVerts));

            foreach (var obs in obstacles ?? Enumerable.Empty<NavPolygon>())
            {
                var holeVerts = obs.Vertices
                    .Select(v => new Vertex(v.X, v.Z))
                    .ToList();
                float cx = obs.Vertices.Sum(v => v.X) / obs.Vertices.Count;
                float cz = obs.Vertices.Sum(v => v.Z) / obs.Vertices.Count;
                input.Add(new Contour(holeVerts), new Point(cx, cz));
            }

            var options = new ConstraintOptions { ConformingDelaunay = false };
            var tMesh = (TNetMesh)input.Triangulate(options);

            return Convert(tMesh);
        }

        private static NavMesh2D Convert(TNetMesh tMesh)
        {
            // Build vertex list and ID→index map
            var tVerts = tMesh.Vertices.ToList();
            var vertexIndex = new Dictionary<int, int>(tVerts.Count);
            var vertices = new List<NavVec2>(tVerts.Count);
            for (int i = 0; i < tVerts.Count; i++)
            {
                vertexIndex[tVerts[i].ID] = i;
                vertices.Add(new NavVec2((float)tVerts[i].X, (float)tVerts[i].Y));
            }

            // Build triangle list and ID→index map
            // Triangle IDs are 1-based; map them to 0-based list indices
            var tTris = tMesh.Triangles.ToList();
            var triIndex = new Dictionary<int, int>(tTris.Count);
            for (int i = 0; i < tTris.Count; i++)
                triIndex[tTris[i].ID] = i;

            var triangles = new List<Pathfinding.Data.NavTriangle>(tTris.Count);
            for (int i = 0; i < tTris.Count; i++)
            {
                var t = tTris[i];
                int v0 = vertexIndex[t.GetVertex(0).ID];
                int v1 = vertexIndex[t.GetVertex(1).ID];
                int v2 = vertexIndex[t.GetVertex(2).ID];

                // GetNeighborID returns -1 for boundary edges, otherwise the neighbor triangle's ID
                int nbId0 = t.GetNeighborID(0);
                int nbId1 = t.GetNeighborID(1);
                int nbId2 = t.GetNeighborID(2);

                int n0 = (nbId0 >= 0 && triIndex.TryGetValue(nbId0, out int mapped0)) ? mapped0 : -1;
                int n1 = (nbId1 >= 0 && triIndex.TryGetValue(nbId1, out int mapped1)) ? mapped1 : -1;
                int n2 = (nbId2 >= 0 && triIndex.TryGetValue(nbId2, out int mapped2)) ? mapped2 : -1;

                triangles.Add(new Pathfinding.Data.NavTriangle(i, v0, v1, v2, n0, n1, n2));
            }

            return new NavMesh2D(vertices, triangles);
        }
    }
}
