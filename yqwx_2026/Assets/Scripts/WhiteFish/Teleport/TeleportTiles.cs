using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//传送地砖：一张 Tilemap 上用两种 Tile 区分 Enter / Exit。
//
// 配对规则：本房间的 Enter ↔ 对方房间的 Exit，本房间的 Exit ↔ 对方房间的 Enter。
// 触发规则：玩家朝地砖迈步（TryWalk 的目标格是传送砖）就传送，人不会真的踩上去，
//           所以站在落点砖上不会被立刻弹回去。
public class TeleportTiles : MonoBehaviour
{
    public enum Kind
    {
        None,
        Enter,
        Exit
    }

    [Header("本房间")]
    [SerializeField] private Grid grid;//本房间的 Grid，必须显式指定
    [SerializeField] private Tilemap tiles;//传送砖所在的 Tilemap

    [Header("地砖")]
    [SerializeField] private TileBase enterTile;//Enter 类别用的 Tile
    [SerializeField] private TileBase exitTile;//Exit 类别用的 Tile

    [Header("配对")]
    [SerializeField] private TeleportTiles partner;//另一个房间的传送砖组

    [Header("节奏")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration = 0.35f;

    private static readonly List<TeleportTiles> all = new List<TeleportTiles>();

    private bool isTeleporting;

    public Grid Grid => grid;
    public TeleportTiles Partner => partner;

    //查询某一格属于哪个传送砖组（含类别），不在任何砖上则返回 null
    public static TeleportTiles At(Vector2Int target, out Kind kind)
    {
        kind = Kind.None;

        for (int i = 0; i < all.Count; i++)
        {
            TeleportTiles group = all[i];
            if (group == null)
                continue;

            Kind found = group.KindAt(target);
            if (found == Kind.None)
                continue;

            kind = found;
            return group;
        }

        return null;
    }

    //按世界坐标查：一个场景里有多个 Grid 时，各组的格子坐标会重复，
    //用世界坐标让每个组用自己的 Tilemap 换算，才不会串房间。
    public static TeleportTiles AtWorld(Vector3 world, out Kind kind)
    {
        kind = Kind.None;

        for (int i = 0; i < all.Count; i++)
        {
            TeleportTiles group = all[i];
            if (group == null || group.tiles == null)
                continue;

            Vector3Int cell = group.tiles.WorldToCell(world);
            Kind found = group.KindAt(new Vector2Int(cell.x, cell.y));
            if (found == Kind.None)
                continue;

            kind = found;
            return group;
        }

        return null;
    }

    private void Awake()
    {
        if (grid == null)
            grid = GetComponentInParent<Grid>();

        if (tiles == null)
            tiles = GetComponent<Tilemap>();
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

    //这一格是什么类别
    public Kind KindAt(Vector2Int target)
    {
        if (tiles == null)
            return Kind.None;

        TileBase tile = tiles.GetTile(new Vector3Int(target.x, target.y, 0));

        if (tile == null)
            return Kind.None;

        if (enterTile != null && tile == enterTile)
            return Kind.Enter;

        if (exitTile != null && tile == exitTile)
            return Kind.Exit;

        return Kind.None;
    }

    //从本组出发：Enter → 对方的 Exit，Exit → 对方的 Enter
    public Kind OppositeOf(Kind from)
    {
        return from == Kind.Enter ? Kind.Exit : Kind.Enter;
    }

    //在指定类别里找一格作为落点
    public Vector2Int FindLanding(Kind kind)
    {
        if (tiles == null)
            return Vector2Int.zero;

        foreach (Vector3Int cell in tiles.cellBounds.allPositionsWithin)
        {
            if (KindAt(new Vector2Int(cell.x, cell.y)) == kind)
                return new Vector2Int(cell.x, cell.y);
        }

        return Vector2Int.zero;
    }

    //玩家朝这块砖迈步时调用：只有 Enter 砖会触发，Exit 砖只是落点
    public void Activate(PlayerGridMove player, Kind from)
    {
        if (isTeleporting || player == null || partner == null)
            return;

        if (from != Kind.Enter)
            return;

        StartCoroutine(TeleportRoutine(player));
    }

    private IEnumerator TeleportRoutine(PlayerGridMove player)
    {
        isTeleporting = true;

        PlayerBrain brain = player.GetComponent<PlayerBrain>();
        if (brain != null)
            brain.inputEnabled = false;

        yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        Vector2Int landingCell = partner.FindLanding(Kind.Exit);//永远落到对方的 Exit

        if (partner.Grid != null)
            player.SetGrid(partner.Grid);

        player.TeleportTo(new Vector3Int(landingCell.x, landingCell.y, 0));

        CameraFollow follow = FindFirstObjectByType<CameraFollow>();
        if (follow != null)
            follow.SnapToTarget();

        yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        if (brain != null)
            brain.inputEnabled = true;

        isTeleporting = false;
    }
}
