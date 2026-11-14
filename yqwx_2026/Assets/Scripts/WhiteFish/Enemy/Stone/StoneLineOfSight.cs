using UnityEngine;

//直线视线：判断目标格是否在朝向正前方的 length 格直线内、且中间没有障碍
public static class StoneLineOfSight
{
    public static bool IsVisibleInLine(Grid grid, LayerMask obstacleMask, Vector3Int origin,
        Vector2Int facing, int length, Vector3Int targetCell)
    {
        if (grid == null || facing == Vector2Int.zero || length <= 0)
            return false;

        if (targetCell == origin)//自身格不算"看到"
            return false;

        Vector3 size = grid.cellSize * 0.8f;//缩到 0.8 倍避免相邻格边缘误判

        for (int step = 1; step <= length; step++)
        {
            Vector3Int cell = origin + new Vector3Int(facing.x * step, facing.y * step, 0);

            if (Physics2D.OverlapBox(grid.GetCellCenterWorld(cell), size, 0f, obstacleMask) != null)
                return false;//墙挡住视线，更远的都看不到

            if (cell == targetCell)
                return true;
        }

        return false;
    }
}
