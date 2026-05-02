using Pathfinding.Data;
using Pathfinding.Geometry;
using Xunit;
using System.Linq;

namespace Pathfinding.Tests.Triangulation
{
    public class NavMeshAssetTests
    {
        private static readonly Polygon Square = new Polygon(new[]
        {
            new Vec2(0f, 0f), new Vec2(10f, 0f),
            new Vec2(10f, 10f), new Vec2(0f, 10f),
        });

        private static readonly Polygon Hole = new Polygon(new[]
        {
            new Vec2(3f, 3f), new Vec2(7f, 3f),
            new Vec2(7f, 7f), new Vec2(3f, 7f),
        });

        [Fact]
        public void ToJson_FromJson_RoundTrip_Boundary()
        {
            var asset = NavMeshAsset.FromPolygons(Square);
            var json = asset.ToJson();
            var loaded = NavMeshAsset.Load(json);

            Assert.Equal(4, loaded.GetBoundary().Vertices.Count);
            Assert.Empty(loaded.GetObstacles());
        }

        [Fact]
        public void ToJson_FromJson_RoundTrip_WithHole()
        {
            var asset = NavMeshAsset.FromPolygons(Square, new[] { Hole });
            var json = asset.ToJson();
            var loaded = NavMeshAsset.Load(json);
            var obstacles = loaded.GetObstacles().ToList();

            Assert.Equal(4, loaded.GetBoundary().Vertices.Count);
            Assert.Single(obstacles);
            Assert.Equal(4, obstacles[0].Vertices.Count);
        }

        [Fact]
        public void Build_FromLoadedAsset_ProducesValidMesh()
        {
            var json = NavMeshAsset.FromPolygons(Square, new[] { Hole }).ToJson();
            var mesh = NavMeshAsset.Load(json).Build();

            Assert.True(mesh.Triangles.Count > 0);
            Assert.Equal(-1, mesh.FindTriangle(new Vec2(5f, 5f))); // hole center
            Assert.True(mesh.FindTriangle(new Vec2(1f, 1f)) >= 0); // outside hole
        }
    }
}
