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
    [SerializeField] private Grid targetGrid;//目标房间的 Grid，必须填

    [Header("节奏")]
    [SerializeField] private float fadeOutDuration = 0.35f;//渐黑用时
    [SerializeField] private float fadeInDuration = 0.35f;//渐亮用时

    private static readonly List<TeleportDoor> doors = new List<TeleportDoor>();

    private Vector2Int cell;//所在格子
    private bool isTeleporting;//传送协程进行中

    public Vector2Int Cell => cell;
    public TeleportPoint TargetPoint => targetPoint;

    //查询某格上的传送门，没有则返回 null
    public static TeleportDoor At(Vector2Int target)
    {
        for (int i = 0; i < doors.Count; i++)
        {
            TeleportDoor door = doors[i];
            if (door != null && door.cell == target)
                return door;
        }

        return null;
    }

    private void Awake()
    {
        if (grid == null)
            grid = GetComponentInParent<Grid>();

        Recalculate();
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
