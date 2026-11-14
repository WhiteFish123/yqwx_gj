using UnityEngine;

// 传送点：摆在场景里，玩家走到它覆盖的格子上就自动传送（同场景瞬移）。
//
// 用法（三步）：
//   1. 把这个 prefab 拖进场景（或直接 Add Component → Teleport Point）
//   2. Inspector 里把 Target 拖成"要去的那一个传送点"
//   3. 想调整落点就改 Landing Offset
//
// 说明：
//   - 用"格子"判断有没有踩到，不用物理 Trigger，所以不会因为物理帧而漏判/重复触发
//   - 触发区域默认只有脚下 1 格；Area Size 调大可以让它覆盖多格
//   - 选中的物体在 Scene 视图里会画出黄色触发框和绿色落点，方便摆放
public class TeleportPoint : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("要传送到哪个传送点。留空表示这个点不触发（可以只当落点用）")]
    public TeleportPoint target;

    [Header("触发区域")]
    [Tooltip("触发区域占几格，从本物体所在格开始往 +x / +y 方向扩展。默认 1×1 = 只有脚下这一格")]
    public Vector2Int areaSize = Vector2Int.one;

    [Header("作为落点")]
    [Tooltip("别人传到这个点时，玩家出现在相对本格的偏移。默认 (0,0) = 直接站在传送阵上")]
    public Vector2Int landingOffset = Vector2Int.zero;

    private Grid grid;

    /// <summary>本物体所在格</summary>
    public Vector3Int Cell => grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;

    /// <summary>别人传过来时玩家站的格</summary>
    public Vector3Int LandingCell =>
        Cell + new Vector3Int(landingOffset.x, landingOffset.y, 0);

    private void Awake()
    {
        grid = FindFirstObjectByType<Grid>();
    }

    private void OnEnable()
    {
        TeleportService.Register(this);
    }

    private void OnDestroy()
    {
        TeleportService.Unregister(this);
    }

    /// <summary>这一格在不在触发区域内</summary>
    public bool ContainsCell(Vector3Int cell)
    {
        Vector3Int origin = Cell;

        if (cell.z != origin.z)
            return false;

        int width = Mathf.Max(1, areaSize.x);
        int height = Mathf.Max(1, areaSize.y);

        return cell.x >= origin.x && cell.x < origin.x + width
            && cell.y >= origin.y && cell.y < origin.y + height;
    }

    //在 Scene 视图里可视化：黄框 = 触发区域，绿点 = 落点
    private void OnDrawGizmos()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid>();
        if (grid == null) return;

        Vector3Int origin = Cell;
        Vector3 cellSize = grid.cellSize;

        Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);

        for (int x = 0; x < Mathf.Max(1, areaSize.x); x++)
        {
            for (int y = 0; y < Mathf.Max(1, areaSize.y); y++)
            {
                Vector3 center = grid.GetCellCenterWorld(origin + new Vector3Int(x, y, 0));
                Gizmos.DrawWireCube(center, cellSize * 0.9f);
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(grid.GetCellCenterWorld(LandingCell), cellSize.x * 0.15f);
    }
}
