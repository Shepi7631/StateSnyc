using System.Collections.Generic;
using Pathfinding.Data;
using Pathfinding.Algorithm;
using Xunit;

namespace Pathfinding.Tests.Algorithm
{
    public class AStarSolverTests
    {
        private static readonly Polygon Square = new Polygon(new[]
        {
            new Vec2(0f, 0f),
            new Vec2(10f, 0f),
            new Vec2(10f, 10f),
            new Vec2(0f, 10f),
        });

        private static readonly Polygon InnerSquare = new Polygon(new[]
        {
            new Vec2(3f, 3f),
            new Vec2(7f, 3f),
            new Vec2(7f, 7f),
            new Vec2(3f, 7f),
        });

        [Fact]
        public void Solve_StartOutsideMesh_ReturnsNull()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var path = AStarSolver.Solve(mesh, new Vec2(-5f, -5f), new Vec2(5f, 5f));
            Assert.Null(path);
        }

        [Fact]
        public void Solve_GoalOutsideMesh_ReturnsNull()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var path = AStarSolver.Solve(mesh, new Vec2(5f, 5f), new Vec2(15f, 15f));
            Assert.Null(path);
        }

        [Fact]
        public void Solve_SameTriangle_ReturnsSingleElement()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var p1 = new Vec2(1f, 1f);
            int tri1 = mesh.FindTriangle(p1);
            Assert.True(tri1 >= 0);
            // Centroid of a triangle is guaranteed to lie inside it,
            // so both points must map to the same triangle.
            Vec2 p2 = mesh.Triangles[tri1].Centroid(mesh.Vertices);

            var path = AStarSolver.Solve(mesh, p1, p2);

            Assert.NotNull(path);
            Assert.Single(path!);
            Assert.Equal(tri1, path![0]);
        }

        [Fact]
        public void Solve_RectangleCorners_ReturnsConnectedPath()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var start = new Vec2(1f, 1f);
            var goal = new Vec2(9f, 9f);

            var path = AStarSolver.Solve(mesh, start, goal);

            Assert.NotNull(path);
            Assert.True(path!.Count >= 1);
            Assert.Equal(mesh.FindTriangle(start), path[0]);
            Assert.Equal(mesh.FindTriangle(goal), path[path.Count - 1]);
            AssertPathConnected(mesh, path);
        }

        [Fact]
        public void Solve_AroundObstacle_PathAvoidsHole()
        {
            var mesh = NavMeshBuilder.Build(Square, new[] { InnerSquare });
            var start = new Vec2(1f, 5f);
            var goal = new Vec2(9f, 5f);

            var path = AStarSolver.Solve(mesh, start, goal);

            Assert.NotNull(path);
            AssertPathConnected(mesh, path!);
            foreach (int triIdx in path!)
            {
                var centroid = mesh.Triangles[triIdx].Centroid(mesh.Vertices);
                bool insideHole = centroid.X > 3f && centroid.X < 7f
                               && centroid.Z > 3f && centroid.Z < 7f;
                Assert.False(insideHole,
                    $"Triangle {triIdx} centroid {centroid} is inside the hole");
            }
        }

        private static void AssertPathConnected(NavMesh2D mesh, IReadOnlyList<int> path)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                var t = mesh.Triangles[path[i]];
                int next = path[i + 1];
                Assert.True(
                    t.N0 == next || t.N1 == next || t.N2 == next,
                    $"Triangle {path[i]} is not a neighbor of {next}");
            }
        }
    }
}
