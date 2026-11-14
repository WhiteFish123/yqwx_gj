using UnityEngine;

//追逐状态：每帧重新寻路到玩家相邻格，看不到玩家就回待机
public class Enemy_ChaseState : Enemy_PathMoveState
{
    private const int lockRange = 3;//锁定范围：这么多格内即使背对也继续追

    public Enemy_ChaseState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }

    protected override float StepDuration => enemy.chaseDuration;//追逐时每格更快

    public override void Update()//正前方两格时改为冲刺攻击
    {
        if (enemy.attackState != null && CanTriggerAttack())
        {
            stateMachine.ChangeState(enemy.attackState);
            return;
        }

        base.Update();
    }

    private bool CanTriggerAttack()//玩家是否正好在朝向的正前方，且中间无遮挡
    {
        if (!enemy.CanAttack())//冷却未结束
            return false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        Vector3Int self = enemy.WorldToCell(enemy.transform.position);
        Vector3Int target = enemy.WorldToCell(player.transform.position);
        Vector3Int delta = target - self;

        Vector3Int wanted = new Vector3Int(enemy.facing.x * enemy.attackTriggerDistance,
                                           enemy.facing.y * enemy.attackTriggerDistance, 0);

        if (delta != wanted)//不在正前方，或距离不对
            return false;

        Vector3Int mid = self + new Vector3Int(enemy.facing.x, enemy.facing.y, 0);
        return !enemy.CellHasObstacle(mid);//中间格不能被挡
    }

    protected override bool ShouldAbort()//看不到玩家就回待机
    {
        if (enemy.CanSeePlayer())
            return false;

        if (IsPlayerWithin(lockRange))//贴得够近就持续锁定，避免玩家绕到背后立刻丢失
            return false;

        stateMachine.ChangeState(enemy.idleState);
        return true;
    }

    protected override Vector3Int? GetTargetCell()//停在玩家四向相邻格
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return null;

        Vector3Int start = enemy.WorldToCell(enemy.transform.position);
        Vector3Int goal = enemy.WorldToCell(player.transform.position);
        return enemy.pathfinder.FindAdjacentGoal(start, goal);
    }

    protected override void OnArrived()//到位后转身面向玩家，保持视野
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Vector3 delta = player.transform.position - enemy.transform.position;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            enemy.SetFacing(delta.x >= 0f ? Vector2Int.right : Vector2Int.left);
        else
            enemy.SetFacing(delta.y >= 0f ? Vector2Int.up : Vector2Int.down);
    }

    private bool IsPlayerWithin(int range)//玩家是否在给定曼哈顿距离内
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        Vector3Int self = enemy.WorldToCell(enemy.transform.position);
        Vector3Int target = enemy.WorldToCell(player.transform.position);
        return Mathf.Abs(self.x - target.x) + Mathf.Abs(self.y - target.y) <= range;
    }
}
