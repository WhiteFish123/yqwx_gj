using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//巡逻路线：把 Tilemap 上的路径格子串成一条有序路线
public class PatrolRoute : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;//路径所在的格子地图
    [SerializeField] private bool hideTilemapInEditor;//编辑模式下也隐藏地砖，只留 Scene 视图里的 Gizmo

    private void Awake()
    {
        if (tilemap == null)
            tilemap = GetComponent<Tilemap>();
    }

    private void OnEnable()//让路径只在 Scene 视图可见
    {
        ApplyRendererVisibility();
    }

    private void OnValidate()//Inspector 改开关时立刻生效
    {
        ApplyRendererVisibility();
    }

    private void ApplyRendererVisibility()
    {
        if (!Application.isPlaying && !hideTilemapInEditor)
        {
            TilemapRenderer shown = GetComponent<TilemapRenderer>();
            if (shown != null)
                shown.enabled = true;
            return;
        }

        TilemapRenderer hidden = GetComponent<TilemapRenderer>();
        if (hidden != null)
            hidden.enabled = false;
    }

    private void OnDrawGizmos()//在 Scene 视图画出路径格子与相邻连线
    {
        if (tilemap == null)
            tilemap = GetComponent<Tilemap>();
        if (tilemap == null)
            return;

        List<Vector3Int> cells = new List<Vector3Int>();//所有路径格子
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell))
                cells.Add(cell);
        }

        if (cells.Count == 0)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);

        Vector3 size = new Vector3(tilemap.cellSize.x * 0.9f, tilemap.cellSize.y * 0.9f, 0f);
        foreach (Vector3Int cell in cells)
        {
            Vector3 center = tilemap.GetCellCenterWorld(cell);
            Gizmos.DrawWireCube(center, size);

            if (cells.Contains(cell + Vector3Int.right))
                Gizmos.DrawLine(center, tilemap.GetCellCenterWorld(cell + Vector3Int.right));
            if (cells.Contains(cell + Vector3Int.up))
                Gizmos.DrawLine(center, tilemap.GetCellCenterWorld(cell + Vector3Int.up));
        }
    }

    //构建路线：points 为按行走顺序排列的格中心世界坐标，isLoop 表示首尾相邻
    public bool TryBuildPath(Vector3 fromWorld, out List<Vector3> points, out bool isLoop)
    {
        points = null;
        isLoop = false;

        if (tilemap == null)//编辑模式或懒加载时补抓
            tilemap = GetComponent<Tilemap>();

        if (tilemap == null)
        {
            Debug.LogError("巡逻路线缺少 Tilemap");
            return false;
        }

        tilemap.CompressBounds();

        HashSet<Vector3Int> cells = new HashSet<Vector3Int>();//所有路径格子
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell))
                cells.Add(cell);
        }

        if (cells.Count == 0)
        {
            Debug.LogError("巡逻路径 Tilemap 上没有格子");
            return false;
        }

        if (cells.Count < 2)
        {
            Debug.LogError("巡逻路径至少需要两个格子");
            return false;
        }

        Vector3Int start = FindNearestCell(cells, fromWorld);//离实体最近的格子

        HashSet<Vector3Int> connected = CollectConnected(cells, start);//起点所在连通块
        if (connected.Count < cells.Count)
            Debug.LogWarning("巡逻路径有 " + (cells.Count - connected.Count) + " 个孤立格子被忽略");

        if (connected.Count < 2)
        {
            Debug.LogError("起点所在连通块只有一格，无法构成路线");
            return false;
        }

        if (TryGetDegrees(connected, out Dictionary<Vector3Int, int> degrees) == false)
        {
            Debug.LogError("巡逻路径存在分叉，每格最多连接两个格子");
            return false;
        }

        List<Vector3Int> order = BuildOrder(connected, degrees, start);
        if (order == null)
        {
            Debug.LogError("巡逻路径无法串成一条连续路线");
            return false;
        }

        isLoop = IsNeighbor(order[0], order[order.Count - 1]);

        points = new List<Vector3>(order.Count);
        foreach (Vector3Int cell in order)
            points.Add(tilemap.GetCellCenterWorld(cell));

        return true;
    }

    private Vector3Int FindNearestCell(HashSet<Vector3Int> cells, Vector3 fromWorld)//找离起点最近的格子
    {
        Vector3Int nearest = default;
        float best = float.MaxValue;

        foreach (Vector3Int cell in cells)
        {
            float distance = (tilemap.GetCellCenterWorld(cell) - fromWorld).sqrMagnitude;
            if (distance < best || (distance == best && IsLess(cell, nearest)))//平手时取字典序更小的，保证结果确定
            {
                best = distance;
                nearest = cell;
            }
        }

        return nearest;
    }

    private static HashSet<Vector3Int> CollectConnected(HashSet<Vector3Int> cells, Vector3Int start)//四邻居连通块
    {
        HashSet<Vector3Int> connected = new HashSet<Vector3Int> { start };
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            foreach (Vector3Int next in Neighbors(current))
            {
                if (cells.Contains(next) && connected.Add(next))
                    queue.Enqueue(next);
            }
        }

        return connected;
    }

    private static bool TryGetDegrees(HashSet<Vector3Int> connected, out Dictionary<Vector3Int, int> degrees)//统计每格邻居数
    {
        degrees = new Dictionary<Vector3Int, int>(connected.Count);

        foreach (Vector3Int cell in connected)
        {
            int degree = 0;
            foreach (Vector3Int next in Neighbors(cell))
            {
                if (connected.Contains(next))
                    degree++;
            }

            if (degree > 2)
                return false;

            degrees[cell] = degree;
        }

        return true;
    }

    private static List<Vector3Int> BuildOrder(HashSet<Vector3Int> connected, Dictionary<Vector3Int, int> degrees, Vector3Int start)//从端点或最小格串成链
    {
        Vector3Int head = start;
        bool hasEnd = false;

        foreach (Vector3Int cell in connected)
        {
            if (degrees[cell] == 1 && (!hasEnd || IsLess(cell, head)))
            {
                head = cell;
                hasEnd = true;
            }
        }

        if (hasEnd == false)//没有端点是环，从字典序最小的格子开始
        {
            head = start;
            foreach (Vector3Int cell in connected)
            {
                if (IsLess(cell, head))
                    head = cell;
            }
        }

        List<Vector3Int> order = new List<Vector3Int>(connected.Count);
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Vector3Int current = head;

        while (true)
        {
            order.Add(current);
            visited.Add(current);

            bool moved = false;
            foreach (Vector3Int next in Neighbors(current))
            {
                if (connected.Contains(next) && visited.Contains(next) == false)
                {
                    current = next;
                    moved = true;
                    break;
                }
            }

            if (moved == false)
                break;
        }

        return order.Count == connected.Count ? order : null;
    }

    private static IEnumerable<Vector3Int> Neighbors(Vector3Int cell)//四向邻居
    {
        yield return cell + Vector3Int.up;
        yield return cell + Vector3Int.down;
        yield return cell + Vector3Int.left;
        yield return cell + Vector3Int.right;
    }

    private static bool IsNeighbor(Vector3Int a, Vector3Int b)//是否四向相邻
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private static bool IsLess(Vector3Int a, Vector3Int b)//字典序：先比 x 再比 y
    {
        return a.x != b.x ? a.x < b.x : a.y < b.y;
    }
}
