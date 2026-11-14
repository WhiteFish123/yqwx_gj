using System.Collections.Generic;
using UnityEngine;

//敌人视野：按朝向扫描前方矩形格子，逐排裁剪被障碍物遮挡的部分
public class EnemyVision
{
    private readonly Grid grid;//所在网格
    private readonly LayerMask obstacleMask;//障碍物层
    private readonly int length;//朝向前方的格数
    private readonly int halfWidth;//垂直方向的半宽（1 表示共 3 排）

    public EnemyVision(Grid grid, LayerMask obstacleMask, int length = 7, int halfWidth = 1)
    {
        this.grid = grid;
        this.obstacleMask = obstacleMask;
        this.length = length;
        this.halfWidth = halfWidth;
    }

    //返回视野内所有可见格子（已按遮挡裁剪），不含起始格
    public List<Vector2Int> GetVisibleCells(Vector3Int origin, Vector2Int facing)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (grid == null || facing == Vector2Int.zero)
            return result;

        Vector2Int side = new Vector2Int(-facing.y, facing.x);//朝向的右侧法线

        for (int offset = -halfWidth; offset <= halfWidth; offset++)//逐排扫描
        {
            Vector3Int lineStart = origin + new Vector3Int(side.x * offset, side.y * offset, 0);

            for (int step = 1; step <= length; step++)//沿朝向推进
            {
                Vector3Int cell = lineStart + new Vector3Int(facing.x * step, facing.y * step, 0);

                if (IsBlocked(cell))
                    break;//这一排被挡住，同排更远的都看不到

                result.Add(new Vector2Int(cell.x, cell.y));
            }
        }

        return result;
    }

    //格子中心是否有障碍物（缩到 0.8 倍避免相邻格边缘误判）
    public bool IsBlocked(Vector3Int cell)
    {
        if (grid == null)
            return false;

        Vector3 center = grid.GetCellCenterWorld(cell);
        Vector3 size = grid.cellSize * 0.8f;
        return Physics2D.OverlapBox(center, size, 0f, obstacleMask) != null;
    }
}
