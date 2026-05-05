using System.Collections.Generic;
using Pathfinding.Data;

namespace Pathfinding.Algorithm
{
    public static class FunnelSolver
    {
        /// <summary>
        /// Simple Stupid Funnel Algorithm. Takes a triangle corridor produced by
        /// AStarSolver and returns the shortest piecewise-linear path from start
        /// to goal that stays within the corridor. Output always starts with
        /// start and ends with goal.
        /// </summary>
        public static IReadOnlyList<Vec2> Solve(
            NavMesh2D mesh,
            IReadOnlyList<int> corridor,
            Vec2 start,
            Vec2 goal)
        {
            if (corridor.Count <= 1)
                return new[] { start, goal };

            int portalCount = corridor.Count - 1;
            var lefts = new Vec2[portalCount + 1];
            var rights = new Vec2[portalCount + 1];
            for (int i = 0; i < portalCount; i++)
            {
                (lefts[i], rights[i]) = _OrientedPortal(mesh, corridor[i], corridor[i + 1]);
            }
            // Terminator: degenerate "portal" where both sides are goal.
            lefts[portalCount] = goal;
            rights[portalCount] = goal;

            var path = new List<Vec2> { start };

            Vec2 apex = start;
            Vec2 portalLeft = lefts[0];
            Vec2 portalRight = rights[0];
            int apexIdx = 0;
            int leftIdx = 0;
            int rightIdx = 0;

            int i2 = 1;
            while (i2 <= portalCount)
            {
                Vec2 newLeft = lefts[i2];
                Vec2 newRight = rights[i2];

                // Update right: newRight tightens when it is CCW from portalRight about apex.
                // 新的右边界必须在当前右边界的逆时针方向上，即更偏左，才能收紧右边界
                if (_TriArea2x(apex, portalRight, newRight) >= 0f)
                {
                    // 三种情况下确定新的右边界：
                    // 1. 当前拐点和当前右边界重合（同侧退化，喇叭右半边无方向）
                    // 2. 当前拐点和当前左边界重合（对侧退化，左边界无方向可越）
                    // 3. 新的右边界在当前左边界的顺时针方向上，即右边
                    if (apex.Equals(portalRight)
                        || apex.Equals(portalLeft)
                        || _TriArea2x(apex, portalLeft, newRight) < 0f)
                    {
                        portalRight = newRight;
                        rightIdx = i2;
                    }
                    // 或者是当前的右边界在左边界的逆时针方向上，即左边，说明此时无法再收紧了，可以确定下一个新的拐点了
                    else
                    {
                        // newRight crossed past the left boundary — emit left as new apex.
                        path.Add(portalLeft);
                        apex = portalLeft;
                        apexIdx = leftIdx;
                        portalLeft = apex;
                        portalRight = apex;
                        leftIdx = apexIdx;
                        rightIdx = apexIdx;
                        i2 = apexIdx + 1;
                        continue;
                    }
                }

                // Update left: newLeft tightens when it is CW from portalLeft about apex.
                // 新的左边界必须在当前左边界的顺时针方向上，即更偏右，才能收紧左边界
                if (_TriArea2x(apex, portalLeft, newLeft) <= 0f)
                {
                    // 三种情况下确定新的左边界：
                    // 1. 当前拐点和当前左边界重合（同侧退化）
                    // 2. 当前拐点和当前右边界重合（对侧退化，右边界无方向可越）
                    // 3. 新的左边界在当前右边界的逆时针方向上，即左边
                    if (apex.Equals(portalLeft)
                        || apex.Equals(portalRight)
                        || _TriArea2x(apex, portalRight, newLeft) > 0f)
                    {
                        portalLeft = newLeft;
                        leftIdx = i2;
                    }
                    else
                    {
                        path.Add(portalRight);
                        apex = portalRight;
                        apexIdx = rightIdx;
                        portalLeft = apex;
                        portalRight = apex;
                        leftIdx = apexIdx;
                        rightIdx = apexIdx;
                        i2 = apexIdx + 1;
                        continue;
                    }
                }

                i2++;
            }

            if (!path[path.Count - 1].Equals(goal))
                path.Add(goal);

            return path;
        }

        // Returns (Left, Right) oriented relative to the walking direction from
        // curTri into nextTri. "Left" is the portal vertex on the left-hand side
        // of the walker; "Right" is on the right-hand side.
        private static (Vec2 Left, Vec2 Right) _OrientedPortal(NavMesh2D mesh, int curTri, int nextTri)
        {
            var tri = mesh.Triangles[curTri];
            int slot;
            if (tri.N0 == nextTri)
                slot = 0;
            else if (tri.N1 == nextTri)
                slot = 1;
            else
                slot = 2;

            int apexIdx;
            int aIdx;
            int bIdx;
            switch (slot)
            {
                case 0:
                    apexIdx = tri.V0; aIdx = tri.V1; bIdx = tri.V2;
                    break;
                case 1:
                    apexIdx = tri.V1; aIdx = tri.V0; bIdx = tri.V2;
                    break;
                default:
                    apexIdx = tri.V2; aIdx = tri.V0; bIdx = tri.V1;
                    break;
            }

            Vec2 apex = mesh.Vertices[apexIdx];
            Vec2 a = mesh.Vertices[aIdx];
            Vec2 b = mesh.Vertices[bIdx];

            // TriArea2x(apex, a, b) > 0 means (apex, a, b) is CCW, so b lies to
            // the left of the apex→a ray, i.e. b is on the walker's left when
            // crossing the portal outward from apex.
            return _TriArea2x(apex, a, b) > 0f
                ? (Left: b, Right: a)
                : (Left: a, Right: b);
        }

        // Twice the signed area of triangle (a, b, c). Positive when CCW.
        private static float _TriArea2x(Vec2 a, Vec2 b, Vec2 c)
        {
            return (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
        }
    }
}
