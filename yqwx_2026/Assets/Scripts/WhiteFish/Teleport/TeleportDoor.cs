using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//传送门：玩家朝这扇门迈步（撞上去）就渐黑，然后瞬移到目标传送点的落点。
//
// 为什么不做"走进去再传"：门本身在 Obstacle 层挡路，玩家根本走不进去，
// 所以触发点定在 PlayerGridMove 迈步失败的那一刻——那里会回头问门"要不要传送"。
//
// 关键：一个场景里有多个 Grid（每个房间一个），玩家跨房间后必须把它的 Grid 换成
//       目标房间的 Grid，否则 GetCellCenterWorld 会按旧 Grid 的偏移算错位置。
public class TeleportDoor : MonoBehaviour
{
    [Header("本房间")]
    [SerializeField] private Grid grid;//门自己所在的 Grid

    [Header("目标")]
    [SerializeField] private TeleportPoint targetPoint;//落到哪个传送点

    //目标房间的 Grid：运行时从传送点自动推导，不需要在 Inspector 里填。
    //之前这个字段是手填的，填错了位置看着没错（WorldToCell/GetCellCenterWorld 往返可还原），
    //但玩家会被 SetGrid 绑到错误的坐标空间——楼梯规则和箱子判定都按格子编号算，
    //跨房间后会静默失效，而且没有任何报错。
    private Grid targetGrid;

    [Header("节奏")]
    [SerializeField] private float fadeOutDuration = 0.35f;//渐黑用时
    [SerializeField] private float fadeInDuration = 0.35f;//渐亮用时

    private static readonly List<TeleportDoor> doors = new List<TeleportDoor>();

    private Vector2Int cell;//所在格子
    private bool isTeleporting;//传送协程进行中

    public Vector2Int Cell => cell;
    public TeleportPoint TargetPoint => targetPoint;

    //查询某格上的传送门，没有则返回 null。
    //必须连 Grid 一起比：一个场景里多个房间各有自己的 Grid，格子编号会重复，
    //只比格子编号的话会撞到别的房间的门上。
    public static TeleportDoor At(Grid grid, Vector2Int target)
    {
        if (grid == null)
            return null;

        for (int i = 0; i < doors.Count; i++)
        {
            TeleportDoor door = doors[i];
            if (door != null && door.grid == grid && door.cell == target)
                return door;
        }

        return null;
    }

    //目标房间的 Grid 由传送点自己决定，不靠手填。
    private Grid ResolveTargetGrid()
    {
        if (targetPoint == null)
            return null;

        //传送点如果挂在某个 Grid 下面，那个 Grid 就是目标房间
        Grid parent = targetPoint.GetComponentInParent<Grid>();
        if (parent != null)
            return parent;

        //传送点一般摆在场景根节点，只能按世界坐标挑最近的 Grid（和 PlayerGridMove 同一思路）
        Grid[] grids = FindObjectsByType<Grid>(FindObjectsSortMode.None);
        Grid nearest = null;
        float best = float.MaxValue;

        foreach (Grid candidate in grids)
        {
            float distance = (candidate.transform.position - targetPoint.transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void Awake()
    {
        if (grid == null)
            grid = GetComponentInParent<Grid>();

        Recalculate();

        targetGrid = ResolveTargetGrid();

        if (targetPoint == null)
            Debug.LogWarning($"[TeleportDoor] {name} 没有配置 targetPoint，这扇门永远不会触发。", this);
        else if (targetGrid == null)
            Debug.LogWarning($"[TeleportDoor] {name} 找不到 targetPoint 所属的 Grid，这扇门无法传送。", this);
    }

    private void OnEnable()
    {
        if (!doors.Contains(this))
            doors.Add(this);
    }

    private void OnDisable()
    {
        doors.Remove(this);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (grid == null)
            grid = GetComponentInParent<Grid>();

        Recalculate();
    }

    private void Recalculate()
    {
        if (grid == null)
            return;

        cell = (Vector2Int)grid.WorldToCell(transform.position);
    }

    //玩家撞上来时由 PlayerGridMove 调用
    public void Activate(PlayerGridMove player)
    {
        if (isTeleporting || player == null || targetPoint == null || targetGrid == null)
            return;

        StartCoroutine(TeleportRoutine(player));
    }

    private IEnumerator TeleportRoutine(PlayerGridMove player)
    {
        isTeleporting = true;

        PlayerBrain brain = player.GetComponent<PlayerBrain>();
        if (brain != null)
            brain.inputEnabled = false;//淡出期间不能走

        yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        //先按目标房间的 Grid 算落点，再让玩家切到那个 Grid
        Vector3Int landing = targetGrid.WorldToCell(targetPoint.transform.position);

        player.SetGrid(targetGrid);
        player.TeleportTo(landing);

        CameraFollow follow = FindFirstObjectByType<CameraFollow>();
        if (follow != null)
            follow.SnapToTarget();

        yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        if (brain != null)
            brain.inputEnabled = true;

        isTeleporting = false;
    }
}
