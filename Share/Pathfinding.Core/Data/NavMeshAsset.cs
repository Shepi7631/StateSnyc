using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pathfinding.Algorithm;

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

        public IReadOnlyList<Polygon> GetObstacles() =>
            Obstacles.Select(obs => new Polygon(obs.Select(v => new Vec2(v[0], v[1])))).ToList();

        public NavMesh2D Build() =>
            NavMeshBuilder.Build(GetBoundary(), GetObstacles());

        private static readonly JsonSerializerOptions s_jsonOptions =
            new JsonSerializerOptions { WriteIndented = true };

        public string ToJson() =>
            JsonSerializer.Serialize(this, s_jsonOptions);

        public static NavMeshAsset Load(string json) =>
            JsonSerializer.Deserialize<NavMeshAsset>(json)
            ?? throw new InvalidOperationException("Failed to deserialize NavMeshAsset.");
    }
}
