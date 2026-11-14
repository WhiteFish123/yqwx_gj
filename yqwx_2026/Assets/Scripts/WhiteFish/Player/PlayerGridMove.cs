using System.Collections;
using UnityEngine;

//临时玩家控制：按 WASD（或方向键）上下左右逐格移动
public class PlayerGridMove : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空则自动查找
    [SerializeField] private float moveDuration = 0.15f;//每格移动用时
    [SerializeField] private LayerMask blockingMask;//阻挡层（格子内有这些层的碰撞体时不能走进去）

    private Vector3Int cell;//当前所在格
    private bool isMoving;//移动协程进行中

    private void Start()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid != null)
            cell = grid.WorldToCell(transform.position);
    }

    private void Update()
    {
        if (isMoving || grid == null)
            return;

        Vector2Int direction = ReadInput();
        if (direction == Vector2Int.zero)
            return;

        Vector3Int target = new Vector3Int(cell.x + direction.x, cell.y + direction.y, cell.z);
        if (IsBlocked(target))
            return;

        StartCoroutine(MoveRoutine(target));
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
}
