using UnityEngine;
using UnityEngine.Tilemaps;

// 挂在 Grid 上：把 Floor / SecondFloor / Stairs 三张 Tilemap 转成区域表（RegionMap），
// 并提供与 GridRules 一致的通行查询。
//
// 每块连续地面一个区域号；不同区域之间默认不连通，
// 唯一的例外是楼梯的两端（梯口）。
//
// 没配地图（三张图都空）时，所有查询都返回 true —— 退化成“只受物理阻挡”，
// 这样还没接这个组件的旧场景行为完全不变。
public sealed class GridPassability : MonoBehaviour
{
    [Header("地图图层")]
    [SerializeField] private Tilemap firstFloor;//一楼地面（Floor）
    [SerializeField] private Tilemap secondFloor;//二楼地面（SecondFloor）
    [SerializeField] private Tilemap stairs;//楼梯（Stairs）

    [Header("楼梯")]
    [Tooltip("楼梯轴向：(0,1) 竖向梯，(1,0) 横向梯")]
    [SerializeField] private Vector2Int stairAxis = Vector2Int.up;

    private RegionMap map;

    public RegionMap Map => map;
    public bool HasMap => map != null && map.Count > 0;

    private void Awake()
    {
        Rebuild();
    }

    // 给编辑器工具和测试用：直接塞引用后重建
    public void Configure(Tilemap firstFloorMap, Tilemap secondFloorMap, Tilemap stairsMap, Vector2Int axis)
    {
        firstFloor = firstFloorMap;
        secondFloor = secondFloorMap;
        stairs = stairsMap;
        stairAxis = axis;
        Rebuild();
    }

    // 重新扫描三张 Tilemap 建区域表。改完地图后调一次即可（编辑器里也可以挂 OnValidate 调）
    public void Rebuild()
    {
        if (!TryGetBounds(out BoundsInt bounds))
        {
            map = null;
            return;
        }

        int width = bounds.size.x;
        int height = bounds.size.y;
        CellKind[] kinds = new CellKind[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, bounds.zMin);
                kinds[y * width + x] = KindOf(cell);
            }
        }

        map = RegionMap.Build(kinds, width, height, bounds.min, stairAxis);
    }

    // 角色能不能从 from 朝 dir 走一步
    public bool CanWalk(Vector3Int from, Vector2Int dir)
    {
        if (!HasMap)
            return true;

        Vector3Int to = from + new Vector3Int(dir.x, dir.y, 0);
        return GridRules.CanWalk(map.At(from), map.At(to), dir);
    }

    // 箱子能不能被推进 to 格（跟走路是两条规则，不能互相调）
    public bool CanChestEnter(Vector3Int from, Vector3Int to)
    {
        if (!HasMap)
            return true;

        return GridRules.CanChestEnter(map.At(from), map.At(to));
    }

    // 推完要不要掉
    public bool ShouldFall(Vector3Int from, Vector3Int to)
    {
        if (!HasMap)
            return false;

        return GridRules.ShouldFall(map.At(from), map.At(to));
    }

    // 某格在几楼（1 或 2）；没有地图数据时返回 0
    public int FloorOf(Vector3Int cell)
    {
        return HasMap ? map.At(cell).Floor : 0;
    }

    // 查某格信息（调试用）
    public CellInfo At(Vector3Int cell)
    {
        return HasMap ? map.At(cell) : CellInfo.Blocked;
    }

    // 种类优先级：楼梯 > 二楼 > 一楼。
    // 楼梯优先，避免二楼地面画到梯顶时把最后一级台阶吃掉。
    private CellKind KindOf(Vector3Int cell)
    {
        if (stairs != null && stairs.HasTile(cell))
            return CellKind.Stairs;

        if (secondFloor != null && secondFloor.HasTile(cell))
            return CellKind.Floor2;

        if (firstFloor != null && firstFloor.HasTile(cell))
            return CellKind.Floor1;

        return CellKind.Blocked;
    }

    // 三张图的外接矩形并集；全空则返回 false
    private bool TryGetBounds(out BoundsInt bounds)
    {
        bounds = default;
        bool any = false;

        Tilemap[] tilemaps = { stairs, secondFloor, firstFloor };

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null)
                continue;

            BoundsInt current = tilemap.cellBounds;

            if (current.size.x <= 0 || current.size.y <= 0)
                continue;

            if (!any)
            {
                bounds = current;
                any = true;
                continue;
            }

            Vector3Int min = Vector3Int.Min(bounds.min, current.min);
            Vector3Int max = Vector3Int.Max(bounds.max, current.max);
            bounds = new BoundsInt(min, max - min);
        }

        return any;
    }
}
