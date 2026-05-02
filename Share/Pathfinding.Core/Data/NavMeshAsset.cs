using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pathfinding.Geometry;
using Pathfinding.Triangulation;

namespace Pathfinding.Data
{
    public sealed class NavMeshAsset
    {
        [JsonPropertyName("boundary")]
        public float[][] Boundary { get; set; } = Array.Empty<float[]>();

        [JsonPropertyName("obstacles")]
        public float[][][] Obstacles { get; set; } = Array.Empty<float[][]>();

        public static NavMeshAsset FromPolygons(Polygon boundary, IEnumerable<Polygon>? obstacles = null)
        {
            return new NavMeshAsset
            {
                Boundary = boundary.Vertices
                    .Select(v => new[] { v.X, v.Z })
                    .ToArray(),
                Obstacles = (obstacles ?? Enumerable.Empty<Polygon>())
                    .Select(o => o.Vertices.Select(v => new[] { v.X, v.Z }).ToArray())
                    .ToArray()
            };
        }

        public Polygon GetBoundary() =>
            new Polygon(Boundary.Select(v => new Vec2(v[0], v[1])));

        public IEnumerable<Polygon> GetObstacles() =>
            Obstacles.Select(obs => new Polygon(obs.Select(v => new Vec2(v[0], v[1]))));

        public NavMesh2D Build() =>
            NavMeshBuilder.Build(GetBoundary(), GetObstacles());

        public string ToJson() =>
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

        public static NavMeshAsset Load(string json) =>
            JsonSerializer.Deserialize<NavMeshAsset>(json)
            ?? throw new InvalidOperationException("Failed to deserialize NavMeshAsset.");
    }
}
