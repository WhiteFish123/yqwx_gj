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

    //史莱姆索敌范围：以「朝右」为例
    //  中间 3 排（side = -1/0/+1）：身后 1 格 ~ 前方 3 格
    //  上下 2 排（side = -2/+2）：只有前方 1~3 格（四个角被切掉）
    //整体是 5 排宽、最多 5 格长的扇形，会被障碍物按排裁剪；随朝向旋转
    public List<Vector2Int> GetSlimeSightCells(Vector3Int origin, Vector2Int facing)
    {
        return GetFanSightCells(origin, facing, 3, 1);
    }

    //爱丽丝的索敌范围：同样是「朝右」为基准的扇形，只是更长
    //  中间 3 排（side = -1/0/+1）：身后 1 格 + 前方 1~7 格
    //  上下 2 排（side = -2/+2）：只有前方 1~7 格（四个角被切掉）
    // 整体是 5 排宽、最远 7 格的扇形，会被障碍物按排裁剪；随朝向旋转
    public List<Vector2Int> GetAliceSightCells(Vector3Int origin, Vector2Int facing)
    {
        return GetFanSightCells(origin, facing, 7, 1);
    }

    //扇形索敌：forward = 最远几格，behind = 中间 3 排往身后看几格
    public List<Vector2Int> GetFanSightCells(Vector3Int origin, Vector2Int facing, int forward, int behind)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (grid == null || facing == Vector2Int.zero)
            return result;

        Vector2Int side = new Vector2Int(-facing.y, facing.x);//朝向的右侧法线

        for (int row = -2; row <= 2; row++)
        {
            int firstStep = Mathf.Abs(row) <= 1 ? -Mathf.Max(0, behind) : 1;//中间 3 排含身后，最上/最下 2 排只朝前

            for (int step = firstStep; step <= forward; step++)
            {
                if (step == 0)
                    continue;//自身格不算

                Vector2Int offset = facing * step + side * row;
                Vector3Int cell = origin + new Vector3Int(offset.x, offset.y, 0);

                if (IsBlocked(cell))
                {
                    if (step >= 0)
                        break;//前方被挡住，这一排更远的都看不到

                    continue;//身后那格被挡住不影响前方
                }

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
