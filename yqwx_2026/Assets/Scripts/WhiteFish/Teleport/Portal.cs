using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 传送门：房间之间的"门口"。
//
// 一个连接 = 两道门，互相指向（A.destination = B，B.destination = A）。
// 玩家朝门格迈步就传送，人不会被放到门格上——落点永远是"对方门前一格"，
// 所以不会重复触发，也不会卡在墙里。
//
// 门格怎么来：拖一个 BoxCollider2D 盖住门口的格子，脚本自动把它们吸成格子坐标。
// 门不一定需要美术：地图边缘留个缺口、放个 Server 一样的 Portal 就能当出口。
//
// 为什么不用 Enter/Exit 两种地砖：那层间接只编码了"从哪边来"，
// 真正的信息是"这道门通向哪"，直接两点互指更省事，也不会填错。
public class Portal : MonoBehaviour
{
    [Header("门的范围")]
    [Tooltip("盖住门口格子的 BoxCollider2D（只用来取格子，不参与物理）。留空则用下面手填的格子。")]
    [SerializeField] private BoxCollider2D area;

    [Tooltip("area 留空时，手填的门口格子。")]
    [SerializeField] private List<Vector2Int> manualCells = new List<Vector2Int>();

    [Header("方向")]
    [Tooltip("门朝房间内的方向：从这道门进来的人，落点就在门的这个方向上一格，并朝这个方向站。")]
    [SerializeField] private Vector2Int outward = Vector2Int.down;

    [Header("配对")]
    [Tooltip("对面那道门。两边都要指，传送才是双向的。")]
    [SerializeField] private Portal destination;

    [Header("节奏")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration = 0.35f;

    private static readonly List<Portal> all = new List<Portal>();
    private readonly List<Vector2Int> cells = new List<Vector2Int>();

    private Grid grid;
    private bool isTeleporting;

    public Grid Grid => grid;
    public Portal Destination => destination;
    public Vector2Int Outward => outward;
    public int CellCount => cells.Count;
    public IReadOnlyList<Vector2Int> Cells => cells;

    public bool Contains(Vector2Int cell)
    {
        return cells.Contains(cell);
    }

    // 查某格上是哪道门。必须连 Grid 一起比：多房间场景里格子编号会重复。
    public static bool TryFind(Grid grid, Vector2Int cell, out Portal portal)
    {
        portal = null;

        if (grid == null)
            return false;

        // 播放模式里重新编译会让静态列表被清空（对象不会重新 OnEnable），
        // 这时按场景里的实际对象重建一次，免得门全部失灵。
        if (all.Count == 0)
            RebuildRegistry();

        for (int i = 0; i < all.Count; i++)
        {
            Portal candidate = all[i];
            if (candidate == null || candidate.grid != grid)
                continue;

            if (!candidate.Contains(cell))
                continue;

            portal = candidate;
            return true;
        }

        return false;
    }

    private static void RebuildRegistry()
    {
        all.Clear();

        Portal[] found = FindObjectsByType<Portal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].isActiveAndEnabled)
                all.Add(found[i]);
        }
    }

    private void Awake()
    {
        grid = GetComponentInParent<Grid>();
        RecalculateCells();
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private void OnValidate()
    {
        grid = GetComponentInParent<Grid>();
        RecalculateCells();
    }

    // 把 area 覆盖的格子吸成格子坐标；没 area 就用手里填的
    private void RecalculateCells()
    {
        cells.Clear();

        if (area == null)
        {
            cells.AddRange(manualCells);
            SortCells();
            return;
        }

        if (grid == null)
            return;

        Bounds bounds = area.bounds;

        // BoxCollider2D 的 z 尺寸是 0，直接用 Bounds.Contains 会因 z 对不上而失败，所以只比 xy
        Vector3Int min = grid.WorldToCell(new Vector3(bounds.min.x, bounds.min.y, 0f));
        Vector3Int max = grid.WorldToCell(new Vector3(bounds.max.x, bounds.max.y, 0f));

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector3 center = grid.GetCellCenterWorld(new Vector3Int(x, y, 0));

                if (center.x < bounds.min.x - 0.001f || center.x > bounds.max.x + 0.001f)
                    continue;

                if (center.y < bounds.min.y - 0.001f || center.y > bounds.max.y + 0.001f)
                    continue;

                if (IsCellBlocked(new Vector3Int(x, y, 0)))
                    continue;

                cells.Add(new Vector2Int(x, y));
            }
        }

        SortCells();
    }

    // 按 x 再按 y 排：横着的门从左到右，竖着的门从下到上，两边顺序一致才能按序号对上
    private static int obstacleLayerIndex = -2;

    // A cell that has a wall tile does not count as a doorway: the wall next to the door must not teleport.
    private bool IsCellBlocked(Vector3Int cell)
    {
        if (grid == null)
            return false;

        foreach (Tilemap map in grid.GetComponentsInChildren<Tilemap>(true))
        {
            if (map == null || !map.HasTile(cell))
                continue;

            if (map.name.StartsWith("Walls"))
                return true;
        }

        return false;
    }


    private void SortCells()
    {
        cells.Sort((a, b) =>
        {
            if (a.x != b.x)
                return a.x.CompareTo(b.x);

            return a.y.CompareTo(b.y);
        });
    }

    // 从 sourceCell 进来，落到对面的哪一格
    // 落点 = 对方门格 + 对方朝向，即"门前一格"，永远不是门格本身
    private Vector2Int LandingFor(Vector2Int sourceCell, IReadOnlyList<Vector2Int> sourceCells)
    {
        int index = -1;
        for (int i = 0; i < sourceCells.Count; i++)
        {
            if (sourceCells[i] == sourceCell)
            {
                index = i;
                break;
            }
        }

        int clamped = Mathf.Clamp(index, 0, cells.Count - 1);
        Vector2Int doorCell = cells[clamped];

        return new Vector2Int(doorCell.x + outward.x, doorCell.y + outward.y);
    }

    // 玩家朝门格迈步时由 PlayerGridMove 调用
    public void Activate(PlayerGridMove player, Vector2Int enteredCell)
    {
        if (isTeleporting || player == null)
            return;

        if (destination == null || destination.cells.Count == 0)
        {
            Debug.LogWarning($"[Portal] {name} 没有配对的门（或对面门没有格子），这次传送被忽略。", this);
            return;
        }

        StartCoroutine(TeleportRoutine(player, enteredCell, new List<Vector2Int>(cells)));
    }

    private IEnumerator TeleportRoutine(PlayerGridMove player, Vector2Int enteredCell, List<Vector2Int> sourceCells)
    {
        isTeleporting = true;

        PlayerBrain brain = player.GetComponent<PlayerBrain>();
        if (brain != null)
            brain.inputEnabled = false;//渐黑期间不接受输入

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        Vector2Int landing = destination.LandingFor(enteredCell, sourceCells);

        if (destination.grid != null)
            player.SetGrid(destination.grid);

        player.TeleportTo(new Vector3Int(landing.x, landing.y, 0));
        player.SetFacing(destination.outward);

        CameraFollow follow = FindFirstObjectByType<CameraFollow>();
        if (follow != null)
            follow.SnapToTarget();

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        if (brain != null)
            brain.inputEnabled = true;

        isTeleporting = false;
    }

    private void OnDrawGizmosSelected()
    {
        Grid g = grid != null ? grid : GetComponentInParent<Grid>();
        if (g == null)
            return;

        Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.9f);

        foreach (Vector2Int cell in cells)
        {
            Vector3 center = g.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            Gizmos.DrawWireCube(center, new Vector3(g.cellSize.x, g.cellSize.y, 0.1f));
        }

        if (cells.Count == 0)
            return;

        // 画一条朝房间内的箭头
        Vector3 origin = g.GetCellCenterWorld(new Vector3Int(cells[0].x, cells[0].y, 0));
        Vector3 dir = new Vector3(outward.x, outward.y, 0f);
        Gizmos.DrawLine(origin, origin + dir);

        if (destination != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.DrawLine(transform.position, destination.transform.position);
        }
    }
}
