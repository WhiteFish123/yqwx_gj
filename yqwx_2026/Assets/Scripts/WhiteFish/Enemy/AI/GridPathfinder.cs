using System.Collections.Generic;
using UnityEngine;

//格子寻路：四向 A*，不允许斜走
public class GridPathfinder
{
    private readonly Grid grid;//所在网格
    private readonly LayerMask obstacleMask;//障碍物层
    private readonly int maxNodes;//最多扩展节点数，防止无限搜索

    public GridPathfinder(Grid grid, LayerMask obstacleMask, int maxNodes = 400)
    {
        this.grid = grid;
        this.obstacleMask = obstacleMask;
        this.maxNodes = maxNodes;
    }

    //格子中心是否有障碍物
    public bool IsBlocked(Vector3Int cell)
    {
        if (grid == null)
            return true;

        Vector3 center = grid.GetCellCenterWorld(cell);
        Vector3 size = grid.cellSize * 0.8f;
        return Physics2D.OverlapBox(center, size, 0f, obstacleMask) != null;
    }

    //四向 A*：返回含首尾的格子序列，不可达返回 null
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int goal)
    {
        if (grid == null || IsBlocked(goal))
            return null;

        if (start == goal)
            return new List<Vector3Int> { start };

        List<Vector3Int> open = new List<Vector3Int> { start };//待扩展
        HashSet<Vector3Int> closed = new HashSet<Vector3Int>();//已扩展
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();//父节点
        Dictionary<Vector3Int, int> gScore = new Dictionary<Vector3Int, int> { { start, 0 } };//已走代价
        Dictionary<Vector3Int, int> fScore = new Dictionary<Vector3Int, int> { { start, Heuristic(start, goal) } };//代价加启发

        int expanded = 0;

        while (open.Count > 0)
        {
            Vector3Int current = open[0];//取 f 最小的节点
            int best = fScore[current];

            for (int i = 1; i < open.Count; i++)
            {
                int value = fScore.TryGetValue(open[i], out int stored) ? stored : int.MaxValue;
                if (value < best)
                {
                    best = value;
                    current = open[i];
                }
            }

            if (current == goal)
                return Reconstruct(cameFrom, current);

            open.Remove(current);
            closed.Add(current);
            expanded++;

            if (expanded > maxNodes)
                return null;

            foreach (Vector3Int next in Neighbors(current))
            {
                if (closed.Contains(next) || IsBlocked(next))
                    continue;

                int tentative = gScore[current] + 1;

                if (gScore.TryGetValue(next, out int known) && tentative >= known)
                    continue;

                cameFrom[next] = current;
                gScore[next] = tentative;
                fScore[next] = tentative + Heuristic(next, goal);

                if (!open.Contains(next))
                    open.Add(next);
            }
        }

        return null;
    }

    //目标格的四向邻居中，离起点最近且可站立的格子
    public Vector3Int? FindAdjacentGoal(Vector3Int start, Vector3Int goal)
    {
        Vector3Int? best = null;
        int bestDistance = int.MaxValue;

        foreach (Vector3Int next in Neighbors(goal))
        {
            if (IsBlocked(next))
                continue;

            int distance = Manhatten(start, next);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = next;
            }
        }

        return best;
    }

    private static List<Vector3Int> Reconstruct(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)//回溯路径
    {
        List<Vector3Int> path = new List<Vector3Int> { current };

        while (cameFrom.TryGetValue(current, out Vector3Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static IEnumerable<Vector3Int> Neighbors(Vector3Int cell)//四向邻居
    {
        yield return cell + Vector3Int.up;
        yield return cell + Vector3Int.down;
        yield return cell + Vector3Int.left;
        yield return cell + Vector3Int.right;
    }

    private static int Heuristic(Vector3Int a, Vector3Int b)
    {
        return Manhatten(a, b);
    }

    private static int Manhatten(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
