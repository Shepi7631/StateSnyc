using Pathfinding.Geometry;
using Pathfinding.Triangulation;
using Xunit;

namespace Pathfinding.Tests.Triangulation
{
    public class NavMeshBuilderTests
    {
        // 10×10 square, CCW winding
        private static readonly Polygon Square = new Polygon(new[]
        {
            new Vec2(0f, 0f),
            new Vec2(10f, 0f),
            new Vec2(10f, 10f),
            new Vec2(0f, 10f),
        });

        [Fact]
        public void Build_Rectangle_ProducesTriangles()
        {
            var mesh = NavMeshBuilder.Build(Square);

            Assert.NotNull(mesh);
            Assert.True(mesh.Triangles.Count >= 2, $"Expected >=2 triangles, got {mesh.Triangles.Count}");
            Assert.True(mesh.Vertices.Count >= 4, $"Expected >=4 vertices, got {mesh.Vertices.Count}");
        }

        [Fact]
        public void Build_Rectangle_AllVerticesInBounds()
        {
            var mesh = NavMeshBuilder.Build(Square);

            foreach (var v in mesh.Vertices)
            {
                Assert.InRange(v.X, -0.001f, 10.001f);
                Assert.InRange(v.Z, -0.001f, 10.001f);
            }
        }

        [Fact]
        public void Build_Rectangle_FindTriangleInsideMesh()
        {
            var mesh = NavMeshBuilder.Build(Square);

            int idx = mesh.FindTriangle(new Vec2(5f, 5f));
            Assert.True(idx >= 0, "Center point should be inside a triangle");
        }

        [Fact]
        public void Build_Rectangle_FindTriangleOutsideMesh()
        {
            var mesh = NavMeshBuilder.Build(Square);

            int idx = mesh.FindTriangle(new Vec2(15f, 15f));
            Assert.Equal(-1, idx);
        }
    }
}
