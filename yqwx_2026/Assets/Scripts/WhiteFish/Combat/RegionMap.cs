using System;
using System.Collections.Generic;
using UnityEngine;

// 区域表：把地图切成若干块“连续地面”（一楼 / 各段楼梯 / 二楼），
// 并给每格记下区域号、楼层、是否楼梯。
//
// 区域号只由“种类 + 四连通”决定，与角色状态无关，
// 所以玩家、敌人、寻路、箱子可以共用同一份数据。
public sealed class RegionMap
{
    private readonly Dictionary<Vector3Int, CellInfo> cells = new Dictionary<Vector3Int, CellInfo>();

    public int Count => cells.Count;

    // 查某一格；地图外返回不可进入
    public CellInfo At(Vector3Int cell)
    {
        return cells.TryGetValue(cell, out CellInfo info) ? info : CellInfo.Blocked;
    }

    // 从“种类网格”建区域表：同一种类、四连通的格子算同一区域。
    // kinds 下标 = (y - origin.y) * width + (x - origin.x)
    public static RegionMap Build(CellKind[] kinds, int width, int height, Vector3Int origin, Vector2Int stairAxis)
    {
        if (kinds == null || kinds.Length != width * height)
            throw new ArgumentException("kinds 长度必须等于 width * height", nameof(kinds));

        RegionMap map = new RegionMap();
        bool[] visited = new bool[kinds.Length];
        int nextRegion = 1;

        for (int i = 0; i < kinds.Length; i++)
        {
            if (visited[i] || kinds[i] == CellKind.Blocked)
                continue;// 墙不参与编号

            foreach (int index in FloodFill(kinds, width, height, i, kinds[i], visited))
            {
                int x = index % width;
                int y = index / width;

                Vector3Int cell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                map.cells[cell] = MakeCell(kinds[index], nextRegion, stairAxis);
            }

            nextRegion++;
        }

        return map;
    }

    // 四连通洪泛：从 seed 出发，把所有“同种类且未访问”的格子收集成一块
    private static List<int> FloodFill(CellKind[] kinds, int width, int height, int seed, CellKind kind, bool[] visited)
    {
        List<int> result = new List<int>();
        Stack<int> stack = new Stack<int>();

        visited[seed] = true;
        stack.Push(seed);

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            result.Add(index);

            int x = index % width;
            int y = index / width;

            TryAdd(x + 1, y);
            TryAdd(x - 1, y);
            TryAdd(x, y + 1);
            TryAdd(x, y - 1);
        }

        return result;

        void TryAdd(int nx, int ny)
        {
            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                return;

            int next = ny * width + nx;

            if (visited[next] || kinds[next] != kind)
                return;

            visited[next] = true;
            stack.Push(next);
        }
    }

    // 种类 → 格子信息：楼层只在地面格上有值，楼梯算“非地面”
    private static CellInfo MakeCell(CellKind kind, int region, Vector2Int stairAxis)
    {
        switch (kind)
        {
            case CellKind.Floor1:
                return new CellInfo(region, 1, false, Vector2Int.zero);

            case CellKind.Floor2:
                return new CellInfo(region, 2, false, Vector2Int.zero);

            case CellKind.Stairs:
                return new CellInfo(region, 0, true, stairAxis);

            default:
                return CellInfo.Blocked;
        }
    }
}
