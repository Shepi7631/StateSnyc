using System;
using System.Collections.Generic;
using System.Linq;

namespace Pathfinding.Data
{
    public sealed class Polygon
    {
        public IReadOnlyList<Vec2> Vertices { get; }

        public Polygon(IEnumerable<Vec2> vertices)
        {
            var list = vertices.ToList();
            if (list.Count < 3)
                throw new ArgumentException("A polygon requires at least 3 vertices.", nameof(vertices));
            Vertices = list;
        }
    }
}
