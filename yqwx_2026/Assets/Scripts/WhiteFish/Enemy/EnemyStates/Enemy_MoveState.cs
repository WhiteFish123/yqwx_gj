using UnityEngine;

//巡逻移动状态：逐格沿路线移动，闭环绕圈，线性往返
public class Enemy_MoveState : EnemyState
{
    private int index;//当前目标点下标
    private int step = 1;//往返方向

    private Vector3 stepFrom;//本步起点
    private Vector3 stepTo;//本步终点
    private float progress;//本步进度
    private bool isStepping;//是否正在跨格

    public Enemy_MoveState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        step = 1;
        isStepping = false;
        progress = 0f;

        if (enemy.patrolPoints == null || enemy.patrolPoints.Count < 2)//没有可用路线
        {
            index = 0;
            Debug.LogError("巡逻状态缺少有效路线：" + enemy.name);
            return;
        }

        index = FindClosestIndex();
    }

    public override void Exit()
    {
        isStepping = false;
        progress = 0f;

        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (enemy.chaseState != null && enemy.CanSeePlayer())//巡逻中发现玩家
        {
            stateMachine.ChangeState(enemy.chaseState);
            return;
        }

        if (enemy.patrolPoints == null || enemy.patrolPoints.Count < 2)
            return;

        if (isStepping)//跨格中：只做插值
        {
            progress += Time.deltaTime / Mathf.Max(0.01f, enemy.moveDuration);
            enemy.transform.position = Vector3.Lerp(stepFrom, stepTo, Mathf.Clamp01(progress));

            if (progress >= 1f)
            {
                enemy.transform.position = stepTo;
                isStepping = false;
                progress = 0f;
            }

            return;
        }

        if (index < 0 || index >= enemy.patrolPoints.Count)//路线在运行期被重建
            index = FindClosestIndex();

        Vector3Int currentCell = enemy.WorldToCell(enemy.transform.position);
        Vector3Int targetCell = enemy.WorldToCell(enemy.patrolPoints[index]);

        if (currentCell == targetCell)//到点，换下一个
        {
            AdvanceIndex();
            return;
        }

        Vector3Int delta = targetCell - currentCell;

        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)//路线点不相邻，重新定位
        {
            index = FindClosestIndex();
            return;
        }

        if (enemy.IsCellOccupied(targetCell))//目标格被玩家或别的敌人占着
            return;

        stepFrom = enemy.CellToCenter(currentCell);
        stepTo = enemy.CellToCenter(targetCell);
        progress = 0f;
        isStepping = true;

        enemy.SetFacing(ToFacing(delta));
    }

    private void AdvanceIndex()//推进目标点下标
    {
        index = NextIndex(index, ref step, enemy.patrolPoints.Count, enemy.patrolIsLoop);
    }

    public static int NextIndex(int index, ref int step, int count, bool isLoop)//计算下一个目标点下标
    {
        if (isLoop)
            return (index + 1) % count;

        index += step;
        if (index >= count || index < 0)
        {
            step = -step;
            index += 2 * step;
        }

        return index;
    }

    private int FindClosestIndex()//离当前位置最近的路线点
    {
        if (enemy.patrolPoints == null || enemy.patrolPoints.Count == 0)
            return 0;

        int nearest = 0;
        float best = float.MaxValue;

        for (int i = 0; i < enemy.patrolPoints.Count; i++)
        {
            float distance = (enemy.patrolPoints[i] - enemy.transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    private static Vector2Int ToFacing(Vector3Int delta)//格坐标增量转四向朝向
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x >= 0 ? Vector2Int.right : Vector2Int.left;

        return delta.y >= 0 ? Vector2Int.up : Vector2Int.down;
    }
}
