using System;
using UnityEngine;

// 网格通行规则：纯逻辑，不依赖场景、Tilemap、物理。
// 只回答三个问题：能不能走、箱子能不能推进去、推完要不要掉。
//
// ★ 这三个问题必须分开算：
//   “走”看区域和门，“推”看格子和楼层差，“掉”看楼层差。
//   如果把“推”接到“走”上，二楼推箱子下楼的机制会立刻失效，
//   因为“二楼 → 一楼”在走路规则里本来就不通。
public static class GridRules
{
    // 角色能不能从 from 朝 dir 走一步
    public static bool CanWalk(in CellInfo from, in CellInfo to, Vector2Int dir)
    {
        if (from.Region == 0 || to.Region == 0)
            return false;// 起点或终点不可站（墙、地图外）

        // 同一块地面：随便走（含楼梯内部横向移动、一楼走廊、二楼阳台）
        if (from.Region == to.Region)
            return true;

        // 不同区域：只有楼梯的两端是门
        return IsDoor(from, dir) || IsDoor(to, dir);
    }

    // 箱子能不能被推进 to 格
    public static bool CanChestEnter(in CellInfo from, in CellInfo to)
    {
        if (to.Region == 0)
            return false;// 墙 / 地图外

        if (to.IsStair)
            return false;// 箱子不上楼梯

        return to.Floor <= from.Floor;// 只能同层或往下，不能往楼层高的地方推
    }

    // 箱子从 from 推到 to 之后，要不要掉
    public static bool ShouldFall(in CellInfo from, in CellInfo to)
    {
        return from.Floor >= 2 && to.Floor < from.Floor;
    }

    // 掉落落点：从 pushedTo 往下最多 fallCells 格，遇到不能站的格子就停。
    // 用“遇阻即停”而不是“一路扫到地板”，保证落点只由 fallCells 决定，不随地图瓦片变化。
    public static Vector3Int FallLanding(Vector3Int pushedTo, int fallCells, Func<Vector3Int, bool> isFree)
    {
        Vector3Int landing = pushedTo;

        for (int i = 0; i < fallCells; i++)
        {
            Vector3Int next = new Vector3Int(landing.x, landing.y - 1, landing.z);

            if (isFree == null || !isFree(next))
                break;

            landing = next;
        }

        return landing;
    }

    // 这一格是不是楼梯端头：楼梯格 + 这一步顺着它的轴向。
    // 由于相邻楼梯格同区域，走到这里时说明轴向邻居已经是别的区域，
    // 也就是“梯口”——楼梯只有两个门，正是这个意思。
    private static bool IsDoor(in CellInfo cell, Vector2Int dir)
    {
        if (!cell.IsStair)
            return false;

        return dir.x * cell.Axis.x + dir.y * cell.Axis.y != 0;
    }
}
