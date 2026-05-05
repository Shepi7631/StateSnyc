using System.Collections.Generic;
using Pathfinding.Algorithm;
using Pathfinding.Data;
using Xunit;

namespace Pathfinding.Tests.Algorithm
{
    public class FunnelSolverTests
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
        public void Solve_SingleTriangleCorridor_ReturnsStartGoalLine()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var start = new Vec2(1f, 1f);
            int tri = mesh.FindTriangle(start);
            Assert.True(tri >= 0);
            var goal = mesh.Triangles[tri].Centroid(mesh.Vertices);

            var path = FunnelSolver.Solve(mesh, new[] { tri }, start, goal);

            Assert.Equal(2, path.Count);
            Assert.Equal(start, path[0]);
            Assert.Equal(goal, path[path.Count - 1]);
        }

        [Fact]
        public void Solve_OpenRectangle_ReturnsStraightLine()
        {
            var mesh = NavMeshBuilder.Build(Square);
            var start = new Vec2(1f, 1f);
            var goal = new Vec2(9f, 9f);
            var corridor = AStarSolver.Solve(mesh, start, goal);
            Assert.NotNull(corridor);

            var path = FunnelSolver.Solve(mesh, corridor!, start, goal);

            Assert.Equal(2, path.Count);
            Assert.Equal(start, path[0]);
            Assert.Equal(goal, path[path.Count - 1]);
        }

        [Fact]
        public void Solve_AroundObstacle_BendsAtCorner()
        {
            // 10x10 square with a 4x4 hole centered at (5,5).
            // Walking from (1,5) to (9,5) must detour around one of the hole corners.
            var mesh = NavMeshBuilder.Build(Square, new[] { InnerSquare });
            var start = new Vec2(1f, 5f);
            var goal = new Vec2(9f, 5f);
            var corridor = AStarSolver.Solve(mesh, start, goal);
            Assert.NotNull(corridor);

            var path = FunnelSolver.Solve(mesh, corridor!, start, goal);

            Assert.True(path.Count >= 3, $"expected at least one bend, got {path.Count} points");
            Assert.Equal(start, path[0]);
            Assert.Equal(goal, path[path.Count - 1]);

            // Every intermediate waypoint must be a hole corner (the only concave
            // vertices between the two endpoints).
            for (int i = 1; i < path.Count - 1; i++)
            {
                bool isHoleCorner =
                    (path[i].Equals(new Vec2(3f, 3f))) ||
                    (path[i].Equals(new Vec2(7f, 3f))) ||
                    (path[i].Equals(new Vec2(7f, 7f))) ||
                    (path[i].Equals(new Vec2(3f, 7f)));
                Assert.True(isHoleCorner, $"intermediate waypoint {path[i]} is not a hole corner");
            }

            // Full polyline length must be shorter than the Manhattan detour but
            // longer than the direct line that cuts through the hole.
            float polyLen = _Length(path);
            float direct = start.DistanceTo(goal);
            float detourUpperBound = start.DistanceTo(new Vec2(3f, 3f))
                                   + new Vec2(3f, 3f).DistanceTo(new Vec2(7f, 3f))
                                   + new Vec2(7f, 3f).DistanceTo(goal);
            Assert.True(polyLen > direct, "funnel path must be longer than the blocked direct line");
            Assert.True(polyLen <= detourUpperBound + 0.001f,
                $"funnel path length {polyLen} exceeds the perimeter detour upper bound {detourUpperBound}");
        }

        [Fact]
        public void Solve_PathIsContinuous_NoDuplicatePoints()
        {
            var mesh = NavMeshBuilder.Build(Square, new[] { InnerSquare });
            var start = new Vec2(1f, 5f);
            var goal = new Vec2(9f, 5f);
            var corridor = AStarSolver.Solve(mesh, start, goal);
            Assert.NotNull(corridor);

            var path = FunnelSolver.Solve(mesh, corridor!, start, goal);

            for (int i = 0; i < path.Count - 1; i++)
                Assert.NotEqual(path[i], path[i + 1]);
        }

        private static float _Length(IReadOnlyList<Vec2> path)
        {
            float len = 0f;
            for (int i = 0; i < path.Count - 1; i++)
                len += path[i].DistanceTo(path[i + 1]);
            return len;
        }
    }
}
