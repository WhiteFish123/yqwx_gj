using System.Collections;
using UnityEngine;

//临时玩家控制：按 WASD（或方向键）上下左右逐格移动，按住 F 可抓取正前方的箱子推拉
public class PlayerGridMove : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空则自动查找
    [SerializeField] private float moveDuration = 0.15f;//每格移动用时
    [SerializeField] private LayerMask blockingMask;//阻挡层（格子内有这些层的碰撞体时不能走进去）

    private Vector3Int cell;//当前所在格
    private bool isMoving;//移动协程进行中
    private Chest grabbedChest;//抓住的箱子，null 表示没抓
    private float lockTimer;//后摇锁定剩余时间（秒），>0 时不能开始新的移动

    public Vector2Int Facing { get; set; } = Vector2Int.down;//朝向，决定推拉方向
    public Vector3Int Cell => cell;

    /// <summary>是否处于后摇/僵直锁定中（武器后摇用）</summary>
    public bool IsLocked => lockTimer > 0f;

    /// <summary>锁住移动 seconds 秒：这段时间内不接受新的移动输入（已有的一步会走完）</summary>
    public void LockMovement(float seconds)
    {
        if (seconds > lockTimer) lockTimer = seconds;
    }

    private void Start()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid != null)
            cell = grid.WorldToCell(transform.position);
    }

    private void Update()
    {
        if (lockTimer > 0f)
            lockTimer -= Time.deltaTime;//后摇计时：即使正在移动也要继续倒数

        if (isMoving || grid == null)
            return;

        if (lockTimer > 0f)
            return;//后摇期间不接受新的移动输入

        Vector2Int direction = ReadInput();
        bool holdingGrab = Input.GetKey(KeyCode.F);

        if (grabbedChest == null)
        {
            if (direction != Vector2Int.zero)
                Facing = direction;

            if (holdingGrab && TryGrab())
                return;

            if (direction != Vector2Int.zero)
                TryWalk(direction);

            return;
        }

        if (!holdingGrab)//松开 F 立即释放，当帧不再移动
        {
            grabbedChest = null;
            return;
        }

        if (direction != Vector2Int.zero)
            TryPushOrPull(direction);
    }

    private Vector2Int ReadInput()//读取方向输入（一次一格）
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            return Vector2Int.up;

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            return Vector2Int.down;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            return Vector2Int.left;

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            return Vector2Int.right;

        return Vector2Int.zero;
    }

    private bool TryGrab()//抓住正前方相邻格的箱子
    {
        Vector2Int playerCell = new Vector2Int(cell.x, cell.y);
        Chest chest = Chest.At(playerCell + Facing);
        if (!ChestPushPullRules.CanGrab(playerCell, Facing, chest != null))
            return false;

        grabbedChest = chest;
        return true;
    }

    private void TryPushOrPull(Vector2Int direction)//朝向方向推，反方向拉
    {
        ChestMoveKind kind = ChestPushPullRules.Resolve(Facing, direction);
        if (kind == ChestMoveKind.None)
            return;

        Vector2Int playerCell = new Vector2Int(cell.x, cell.y);
        if (!ChestPushPullRules.CanExecute(playerCell, grabbedChest.Cell, Facing, kind,
                IsCellBlocked, HasOtherChest))
            return;

        ChestPushPullRules.TryComputeTargets(playerCell, grabbedChest.Cell, Facing, kind,
            out Vector2Int playerTarget, out Vector2Int chestTarget);

        StartCoroutine(MoveBothRoutine(playerTarget, grabbedChest, chestTarget));
    }

    private bool IsCellBlocked(Vector2Int target)//墙体/敌人占用，口径与 IsBlocked 一致
    {
        return IsBlocked(new Vector3Int(target.x, target.y, cell.z));
    }

    private bool HasOtherChest(Vector2Int target)//目标格是否被别的箱子占着
    {
        Chest chest = Chest.At(target);
        return chest != null && chest != grabbedChest;
    }

    private void TryWalk(Vector2Int direction)//普通走路
    {
        Vector3Int target = new Vector3Int(cell.x + direction.x, cell.y + direction.y, cell.z);
        if (!IsWalkable(target))
            return;

        StartCoroutine(MoveRoutine(target));
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
