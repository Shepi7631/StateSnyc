using System;
using System.Collections.Generic;

namespace Pathfinding.Data
{
    public sealed class NavTriangle
    {
        public int Index { get; }

        public int V0 { get; }
        public int V1 { get; }
        public int V2 { get; }

        // N0 is opposite V0 (shared edge V1-V2), N1 opposite V1, N2 opposite V2
        // -1 means no neighbor (boundary edge)
        public int N0 { get; }
        public int N1 { get; }
        public int N2 { get; }

        public NavTriangle(int index, int v0, int v1, int v2, int n0, int n1, int n2)
        {
            Index = index;
            V0 = v0; V1 = v1; V2 = v2;
            N0 = n0; N1 = n1; N2 = n2;
        }

        public Vec2 Centroid(IReadOnlyList<Vec2> vertices)
        {
            var a = vertices[V0];
            var b = vertices[V1];
            var c = vertices[V2];
            return new Vec2((a.X + b.X + c.X) / 3f, (a.Z + b.Z + c.Z) / 3f);
        }

        // slot 0 → edge V1-V2, slot 1 → edge V0-V2, slot 2 → edge V0-V1
        public Portal GetPortal(int neighborSlot, IReadOnlyList<Vec2> vertices)
        {
            switch (neighborSlot)
            {
                case 0: return new Portal(vertices[V1], vertices[V2]);
                case 1: return new Portal(vertices[V0], vertices[V2]);
                case 2: return new Portal(vertices[V0], vertices[V1]);
                default: throw new ArgumentOutOfRangeException(nameof(neighborSlot));
            }
        }
    }
}
