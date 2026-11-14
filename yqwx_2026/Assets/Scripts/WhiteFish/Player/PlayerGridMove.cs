using System.Collections;
using UnityEngine;

// 格子移动"引擎"：只提供能力，不做决定。
//
// 和以前的区别：以前这个脚本自己在 Update 里读输入、自己决定走/抓/推；
// 现在这些决定全部交给 PlayerBrain 的状态机（Idle / Move / Attack / Interact），
// 它只负责把"走一格""推一格"这些动作平滑播完。
//
// ★ 推拉箱子的规则完全复用学长的 ChestPushPullRules，这里一行判定逻辑都没有重写。
public class PlayerGridMove : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空则自动查找
    [SerializeField] private float moveDuration = 0.15f;//每格移动用时
    [SerializeField] private LayerMask blockingMask;//阻挡层（格子内有这些层的碰撞体时不能走进去）

    private Vector3Int cell;//当前所在格
    private bool isMoving;//移动协程进行中
    private Chest grabbedChest;//抓住的箱子，null 表示没抓

    public Vector2Int Facing { get; set; } = Vector2Int.down;//朝向（由状态在移动前设置）
    public Vector3Int Cell => cell;//当前所在格
    public bool IsMoving => isMoving;//是否正在跨格
    public bool IsGrabbing => grabbedChest != null;//是否抓着箱子

    private void Start()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid != null)
            cell = grid.WorldToCell(transform.position);
    }

    // ===== 下面这些是给状态机调用的"能力" =====

    //尝试朝 direction 走一格；返回 true 表示真的迈出去了（会在 moveDuration 内走完）
    public bool TryWalk(Vector2Int direction)
    {
        if (isMoving || grid == null || direction == Vector2Int.zero)
            return false;

        Vector3Int target = new Vector3Int(cell.x + direction.x, cell.y + direction.y, cell.z);
        if (!IsWalkable(target))
            return false;

        StartCoroutine(MoveRoutine(target));
        return true;
    }

    //设置朝向（零向量忽略）。抓箱子期间不会被移动改写，推拉方向才稳定。
    public void SetFacing(Vector2Int direction)
    {
        if (direction != Vector2Int.zero)
            Facing = direction;
    }

    //抓住正前方相邻格的箱子
    public bool TryGrab()
    {
        if (isMoving || grabbedChest != null || grid == null)
            return false;

        Vector2Int playerCell = new Vector2Int(cell.x, cell.y);
        Chest chest = Chest.At(playerCell + Facing);

        if (!ChestPushPullRules.CanGrab(playerCell, Facing, chest != null))
            return false;

        grabbedChest = chest;
        return true;
    }

    public void ReleaseGrab()
    {
        grabbedChest = null;
    }

    //朝向方向推、反方向拉（判定与目标格计算全部来自学长的 ChestPushPullRules）
    public bool TryPushOrPull(Vector2Int direction)
    {
        if (isMoving || grabbedChest == null || grid == null)
            return false;

        ChestMoveKind kind = ChestPushPullRules.Resolve(Facing, direction);
        if (kind == ChestMoveKind.None)
            return false;

        Vector2Int playerCell = new Vector2Int(cell.x, cell.y);

        if (!ChestPushPullRules.CanExecute(playerCell, grabbedChest.Cell, Facing, kind,
                IsCellBlocked, HasOtherChest))
            return false;

        ChestPushPullRules.TryComputeTargets(playerCell, grabbedChest.Cell, Facing, kind,
            out Vector2Int playerTarget, out Vector2Int chestTarget);

        StartCoroutine(MoveBothRoutine(playerTarget, grabbedChest, chestTarget));
        return true;
    }

    // ===== 内部工具 =====

    private bool IsCellBlocked(Vector2Int target)//墙体/敌人占用，口径与 IsBlocked 一致
    {
        return IsBlocked(new Vector3Int(target.x, target.y, cell.z));
    }

    private bool HasOtherChest(Vector2Int target)//目标格是否被别的箱子占着
    {
        Chest chest = Chest.At(target);
        return chest != null && chest != grabbedChest;
    }

    private bool IsWalkable(Vector3Int target)//墙、敌人、箱子都不能进
    {
        if (IsBlocked(target))
            return false;

        return Chest.At(new Vector2Int(target.x, target.y)) == null;
    }

    private bool IsBlocked(Vector3Int target)//目标格是否被挡住
    {
        if (blockingMask != 0)
        {
            Vector3 center = grid.GetCellCenterWorld(target);
            Vector3 size = grid.cellSize * 0.8f;

            if (Physics2D.OverlapBox(center, size, 0f, blockingMask) != null)
                return true;
        }

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))//敌人所在格不能进
        {
            if (enemy == null || enemy.isDead)
                continue;

            if (grid.WorldToCell(enemy.transform.position) == target)
                return true;
        }

        return false;
    }

    private IEnumerator MoveRoutine(Vector3Int target)//平滑移动到目标格中心
    {
        isMoving = true;

        Vector3 start = transform.position;
        Vector3 end = grid.GetCellCenterWorld(target);
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / moveDuration);
            yield return null;
        }

        transform.position = end;
        cell = target;
        isMoving = false;
    }

    private IEnumerator MoveBothRoutine(Vector2Int playerTarget, Chest chest, Vector2Int chestTarget)//玩家与箱子同步移动一格
    {
        isMoving = true;

        Vector3Int targetCell = new Vector3Int(playerTarget.x, playerTarget.y, cell.z);
        Vector3 playerStart = transform.position;
        Vector3 playerEnd = grid.GetCellCenterWorld(targetCell);

        chest.MoveTo(chestTarget, moveDuration);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(playerStart, playerEnd, elapsed / moveDuration);
            yield return null;
        }

        transform.position = playerEnd;
        cell = targetCell;
        isMoving = false;
    }
}
