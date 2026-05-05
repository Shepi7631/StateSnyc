using System.Collections.Generic;

namespace Pathfinding.Data
{
    public sealed class NavMesh2D
    {
        public IReadOnlyList<Vec2> Vertices { get; }
        public IReadOnlyList<NavTriangle> Triangles { get; }

        public NavMesh2D(IReadOnlyList<Vec2> vertices, IReadOnlyList<NavTriangle> triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
        }

        // Returns index of the triangle containing point, or -1 if none found.
        public int FindTriangle(Vec2 point)
        {
            for (int i = 0; i < Triangles.Count; i++)
            {
                if (Contains(Triangles[i], point))
                    return i;
            }
            return -1;
        }

        private bool Contains(NavTriangle t, Vec2 p)
        {
            var a = Vertices[t.V0];
            var b = Vertices[t.V1];
            var c = Vertices[t.V2];
            float d0 = Cross(b - a, p - a);
            float d1 = Cross(c - b, p - b);
            float d2 = Cross(a - c, p - c);
            bool hasNeg = d0 < 0f || d1 < 0f || d2 < 0f;
            bool hasPos = d0 > 0f || d1 > 0f || d2 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vec2 a, Vec2 b) => a.X * b.Z - a.Z * b.X;
    }
}
