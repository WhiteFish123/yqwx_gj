using UnityEngine;
using UnityEngine.Tilemaps;

//直线视线：判断目标格是否在朝向正前方�?length 格直线内、且中间没有障碍
//secondFloor 用来挡住跨楼层：起点在二楼就只能看二楼，一楼就只能看一�?
public static class StoneLineOfSight
{
    public static bool IsVisibleInLine(Grid grid, LayerMask obstacleMask, Vector3Int origin,
        Vector2Int facing, int length, Vector3Int targetCell, Tilemap secondFloor = null)
    {
        if (grid == null || facing == Vector2Int.zero || length <= 0)
            return false;

        if (targetCell == origin)//自身格不�?看到"
            return false;

        int originFloor = FloorOf(secondFloor, origin);

        Vector3 size = grid.cellSize * 0.8f;//缩到 0.8 倍避免相邻格边缘误判

        for (int step = 1; step <= length; step++)
        {
            Vector3Int cell = origin + new Vector3Int(facing.x * step, facing.y * step, 0);

            if (FloorOf(secondFloor, cell) != originFloor)
                return false;//跨楼层，视线到此为止

            if (Physics2D.OverlapBox(grid.GetCellCenterWorld(cell), size, 0f, obstacleMask) != null)
                return false;//墙挡住视线，更远的都看不�?

            if (cell == targetCell)
                return true;
        }

        return false;
    }

    private static int FloorOf(Tilemap secondFloor, Vector3Int cell)//�ø����ڼ�¥
    {
        return FloorQuery.FloorOf(secondFloor, cell);//single source of truth
    }
}
