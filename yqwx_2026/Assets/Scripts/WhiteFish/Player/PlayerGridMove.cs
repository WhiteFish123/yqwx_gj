using System.Collections;
using UnityEngine;

// 格子移动"引擎"：只提供能力，不做决定�?
//
// 和以前的区别：以前这个脚本自己在 Update 里读输入、自己决定走/�?推；
// 现在这些决定全部交给 PlayerBrain 的状态机（Idle / Move / Attack / Interact），
// 它只负责�?走一�?"推一�?这些动作平滑播完�?
//
// �?推拉箱子的规则完全复用学长的 ChestPushPullRules，这里一行判定逻辑都没有重写�?
public class PlayerGridMove : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空则自动查�?
    [SerializeField] private float moveDuration = 0.15f;//每格移动用时
    [SerializeField] private float pushPullDuration = 0.3f;//推拉箱子一格用时（比走路慢，动画速率会按比例降低�?
    [SerializeField] private LayerMask blockingMask;//阻挡层（格子内有这些层的碰撞体时不能走进去）

    private Vector3Int cell;//当前所在格
    private bool isMoving;//移动协程进行�?
    private Chest grabbedChest;//抓住的箱子，null 表示没抓

    public Vector2Int Facing { get; set; } = Vector2Int.down;//朝向（由状态在移动前设置）
    public Vector3Int Cell => cell;//当前所在格
    public bool IsMoving => isMoving;//是否正在跨格
    public bool IsGrabbing => grabbedChest != null;//是否抓着箱子
    public bool IsPushing { get; private set; }//是否正在推拉箱子（给动画用）

    //推拉时相对走路的移动速度比例�? = 一样快�?.5 = 慢一半）�?
    //给动画用：推拉比走路慢，动画播放速率跟着按比例降低�?
    public float PushPullSpeedScale =>
        pushPullDuration > 0.001f ? Mathf.Clamp01(moveDuration / pushPullDuration) : 1f;

    private GridPassability passability;//所在 Grid 的区域表，跨房间时同步换

    private void Start()
    {
        if (grid == null)
            grid = FindNearestGrid();

        if (grid != null)
            cell = grid.WorldToCell(transform.position);

        RefreshPassability();
    }

    //区域表跟着当前 Grid 走：多房间场景里每个房间一份
    private void RefreshPassability()
    {
        passability = grid != null ? grid.GetComponent<GridPassability>() : null;
    }

    //一个场景里有多�?Grid（每个房间一个），取离玩家最近的那个�?
    //否则默认会拿到主房间�?Grid，算出来的格子坐标会差几十格�?
    private Grid FindNearestGrid()
    {
        Grid[] grids = FindObjectsByType<Grid>(FindObjectsSortMode.None);
        if (grids.Length == 0)
            return null;

        Grid nearest = grids[0];
        float best = float.MaxValue;

        foreach (Grid candidate in grids)
        {
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    // ===== 下面这些是给状态机调用�?能力" =====

    //尝试�?direction 走一格；返回 true 表示真的迈出去了（会�?moveDuration 内走完）
    public bool TryWalk(Vector2Int direction)
    {
        if (isMoving || grid == null || direction == Vector2Int.zero)
            return false;

        Vector3Int target = new Vector3Int(cell.x + direction.x, cell.y + direction.y, cell.z);

        //朝门口迈步就传送（人不会被放到门格上，所以落点不会立刻反弹）
        Vector2Int targetCell = new Vector2Int(target.x, target.y);
        if (Portal.TryFind(grid, targetCell, out Portal portal))
        {
            portal.Activate(this, targetCell);
            return false;
        }

        if (!IsWalkable(target))
            return false;

        if (passability != null && !passability.CanWalk(cell, direction))
            return false;//楼梯侧面、跨楼层这类“这一步不允许”的边

        StartCoroutine(MoveRoutine(target));
        return true;
    }

    //设置朝向（零向量忽略）。抓箱子期间不会被移动改写，推拉方向才稳定�?
    public void SetFacing(Vector2Int direction)
    {
        if (direction != Vector2Int.zero)
            Facing = direction;
    }

    //切换所在网格：跨房间传送后必须换到目标房间�?Grid�?
    //否则 GetCellCenterWorld 会按�?Grid 的偏移算出错误的世界坐标�?
    public void SetGrid(Grid newGrid)
    {
        if (newGrid != null)
            grid = newGrid;

        RefreshPassability();
    }

    //瞬移：把玩家直接放到某一格的中心�?
    //�?一定要在这里同�?cell，否则内部记录的还是旧格子，
    //  下一次走路会从旧格子算方向，人就会往莫名其妙的地方迈�?
    public void TeleportTo(Vector3Int targetCell)
    {
        StopAllCoroutines();//万一正在跨格，先打断
        isMoving = false;
        IsPushing = false;
        grabbedChest = null;//传送会脱离箱子
        cell = targetCell;

        if (grid != null)
            transform.position = grid.GetCellCenterWorld(targetCell);
    }

    //抓住正前方相邻格的箱�?
    public bool TryGrab()
    {
        if (isMoving || grabbedChest != null || grid == null)
            return false;

        Vector2Int playerCell = new Vector2Int(cell.x, cell.y);
        Chest chest = Chest.At(grid, playerCell + Facing);

        if (chest != null && !OnSameFloor(cell, new Vector3Int(chest.Cell.x, chest.Cell.y, cell.z)))
            return false;//楼上的玩家够不到楼下的箱子

        if (chest != null && chest.IsBusy)//a chest in mid-air cannot be grabbed
            return false;

        if (!ChestPushPullRules.CanGrab(playerCell, Facing, chest != null))
            return false;

        grabbedChest = chest;
        return true;
    }

    public void ReleaseGrab()
    {
        grabbedChest = null;
    }

    //朝向方向推、反方向拉（判定与目标格计算全部来自学长�?ChestPushPullRules�?
    public bool TryPushOrPull(Vector2Int direction)
    {
        if (isMoving || grabbedChest == null || grabbedChest.IsBusy || grid == null)
            return false;

        ChestMoveKind kind = ChestPushPullRules.Resolve(Facing, direction);
        if (kind == ChestMoveKind.None)
            return false;

        Vector2Int playerCell = new Vector2Int(cell.x, cell.y);

        if (!OnSameFloor(cell, new Vector3Int(grabbedChest.Cell.x, grabbedChest.Cell.y, cell.z)))
            return false;

        if (!ChestPushPullRules.TryComputeTargets(playerCell, grabbedChest.Cell, Facing, kind,
                out Vector2Int playerTarget, out Vector2Int chestTarget))
            return false;

        if (!CanChestMove(playerCell, playerTarget, chestTarget))
            return false;

        if (!ChestPushPullRules.CanExecute(playerCell, grabbedChest.Cell, Facing, kind,
                IsCellBlocked, HasOtherChest))
            return false;

        StartCoroutine(MoveBothRoutine(playerTarget, grabbedChest, chestTarget));
        return true;
    }

    // ===== 内部工具 =====

    private bool IsCellBlocked(Vector2Int target)//墙体/敌人占用，口径与 IsBlocked 一�?
    {
        return IsBlocked(new Vector3Int(target.x, target.y, cell.z));
    }

    private bool HasOtherChest(Vector2Int target)//目标格是否被别的箱子占着
    {
        Chest chest = Chest.At(grid, target);
        return chest != null && chest != grabbedChest;
    }

    private bool IsWalkable(Vector3Int target)//墙、敌人、箱子都不能�?
    {
        if (IsBlocked(target))
            return false;

        Chest chest = Chest.At(grid, new Vector2Int(target.x, target.y));

        //另一层地板上的箱子不该挡住这一层
        return chest == null || !OnSameFloor(cell, target);
    }

    //玩家和某格是不是同一楼层（任一边查不到楼层信息时按同一层处理）
    private bool OnSameFloor(Vector3Int from, Vector3Int to)
    {
        if (passability == null)
            return true;

        int fromFloor = passability.FloorOf(from);
        int toFloor = passability.FloorOf(to);

        return fromFloor == 0 || toFloor == 0 || fromFloor == toFloor;
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

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))//敌人所在格不能�?
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

    //推拉时：箱子那一格按“推”的规则判（允许往下推），玩家自己那一格按“走”的规则判
    private bool CanChestMove(Vector2Int playerCell, Vector2Int playerTarget, Vector2Int chestTarget)
    {
        if (passability == null)
            return true;

        Vector3Int chestFrom = new Vector3Int(grabbedChest.Cell.x, grabbedChest.Cell.y, cell.z);
        Vector3Int chestTo = new Vector3Int(chestTarget.x, chestTarget.y, cell.z);

        if (!passability.CanChestEnter(chestFrom, chestTo))
            return false;

        Vector2Int playerStep = playerTarget - playerCell;

        return playerStep == Vector2Int.zero || passability.CanWalk(cell, playerStep);
    }

    private IEnumerator MoveBothRoutine(Vector2Int playerTarget, Chest chest, Vector2Int chestTarget)//玩家与箱子同步移动一�?
    {
        isMoving = true;
        IsPushing = true;

        bool wasOnSecondFloor = chest.IsSecondFloorCell(chest.Cell);//记下推动前箱子是否在二楼
        bool willBeOnSecondFloor = chest.IsSecondFloorCell(chestTarget);
        Vector2Int pushDir = chestTarget - new Vector2Int(chest.Cell.x, chest.Cell.y);//推动方向（掉落伤害矩形按它摆�?

        Vector3Int targetCell = new Vector3Int(playerTarget.x, playerTarget.y, cell.z);
        Vector3 playerStart = transform.position;
        Vector3 playerEnd = grid.GetCellCenterWorld(targetCell);

        chest.MoveTo(chestTarget, pushPullDuration);

        float elapsed = 0f;
        while (elapsed < pushPullDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(playerStart, playerEnd, elapsed / pushPullDuration);
            yield return null;
        }

        transform.position = playerEnd;
        cell = targetCell;
        isMoving = false;
        IsPushing = false;

        if (!wasOnSecondFloor || willBeOnSecondFloor)//没从二楼推出去，正常结束
            yield break;

        while (chest.IsMoving)//等箱子横向到�?
            yield return null;

        ReleaseGrab();//箱子被推出边缘，玩家松手

        Vector2Int landing = chest.FindLandingCell();
        if (landing != chest.Cell)
        {
            chest.SetFallPushDirection(pushDir);
            chest.FallTo(landing);
        }
    }
}
