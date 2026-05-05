using System;
using System.Collections.Generic;
using Pathfinding.Data;

namespace Pathfinding.Algorithm
{
    public static class AStarSolver
    {
        /// <summary>
        /// 寻路核心算法：A*
        /// </summary>
        /// <param name="mesh">图</param>
        /// <param name="start">起点坐标</param>
        /// <param name="goal">终点坐标</param>
        /// <returns>从起点到终点的三角形索引路径（如果存在）</returns>
        public static IReadOnlyList<int>? Solve(NavMesh2D mesh, Vec2 start, Vec2 goal)
        {
            int startTri = mesh.FindTriangle(start);
            int goalTri = mesh.FindTriangle(goal);
            if (startTri < 0 || goalTri < 0)
                return null;
            if (startTri == goalTri)
                return new[] { startTri };

            int count = mesh.Triangles.Count;
            var centroids = _BuildCentroids(mesh);

            var gScore = new float[count];
            var cameFrom = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                gScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }
            gScore[startTri] = 0f;

            var open = new MinHeap(count);
            open.Push(startTri, centroids[startTri].DistanceTo(goal));

            while (open.TryPop(out int cur, out _))
            {
                if (closed[cur])
                    continue;
                if (cur == goalTri)
                    return _Reconstruct(cameFrom, startTri, goalTri);
                closed[cur] = true;

                var t = mesh.Triangles[cur];
                for (int slot = 0; slot < 3; slot++)
                {
                    int nb = _GetNeighbor(t, slot);
                    if (nb < 0 || closed[nb])
                        continue;
                    float tentativeG = gScore[cur] + centroids[cur].DistanceTo(centroids[nb]);
                    if (tentativeG < gScore[nb])
                    {
                        gScore[nb] = tentativeG;
                        cameFrom[nb] = cur;
                        open.Push(nb, tentativeG + centroids[nb].DistanceTo(goal));
                    }
                }
            }
            return null;
        }

        private static Vec2[] _BuildCentroids(NavMesh2D mesh)
        {
            int count = mesh.Triangles.Count;
            var centroids = new Vec2[count];
            for (int i = 0; i < count; i++)
                centroids[i] = mesh.Triangles[i].Centroid(mesh.Vertices);
            return centroids;
        }

        private static int _GetNeighbor(NavTriangle t, int slot)
        {
            switch (slot)
            {
                case 0: return t.N0;
                case 1: return t.N1;
                case 2: return t.N2;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static IReadOnlyList<int> _Reconstruct(int[] cameFrom, int startTri, int goalTri)
        {
            var reverse = new List<int>();
            int cur = goalTri;
            while (cur != -1)
            {
                reverse.Add(cur);
                if (cur == startTri)
                    break;
                cur = cameFrom[cur];
            }
            reverse.Reverse();
            return reverse;
        }

        private sealed class MinHeap
        {
            private (int Idx, float F)[] _Data;
            private int _Count;

            public MinHeap(int capacity)
            {
                _Data = new (int, float)[Math.Max(capacity, 8)];
                _Count = 0;
            }

            public void Push(int idx, float f)
            {
                if (_Count == _Data.Length)
                {
                    var grown = new (int, float)[_Data.Length * 2];
                    Array.Copy(_Data, grown, _Data.Length);
                    _Data = grown;
                }
                _Data[_Count] = (idx, f);
                _SiftUp(_Count);
                _Count++;
            }

            public bool TryPop(out int idx, out float f)
            {
                if (_Count == 0)
                {
                    idx = -1;
                    f = 0f;
                    return false;
                }
                idx = _Data[0].Idx;
                f = _Data[0].F;
                _Count--;
                if (_Count > 0)
                {
                    _Data[0] = _Data[_Count];
                    _SiftDown(0);
                }
                return true;
            }

            private void _SiftUp(int i)
            {
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_Data[i].F < _Data[parent].F)
                    {
                        var tmp = _Data[i];
                        _Data[i] = _Data[parent];
                        _Data[parent] = tmp;
                        i = parent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private void _SiftDown(int i)
            {
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = i * 2 + 2;
                    int smallest = i;
                    if (left < _Count && _Data[left].F < _Data[smallest].F)
                        smallest = left;
                    if (right < _Count && _Data[right].F < _Data[smallest].F)
                        smallest = right;
                    if (smallest == i)
                        break;
                    var tmp = _Data[i];
                    _Data[i] = _Data[smallest];
                    _Data[smallest] = tmp;
                    i = smallest;
                }
            }
        }
    }
}
