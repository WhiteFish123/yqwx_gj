using UnityEngine;

//返回巡逻状态：逐格走回巡逻路线上最近的格子
public class Enemy_ReturnState : Enemy_PathMoveState
{
    public Enemy_ReturnState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }

    protected override float StepDuration => enemy.moveDuration;//返回途中用巡逻速度

    protected override Vector3Int? GetTargetCell()
    {
        return enemy.FindClosestPatrolCell();
    }

    protected override void OnArrived()//回到路线，恢复巡逻
    {
        stateMachine.ChangeState(enemy.moveState);
    }

    protected override void OnNoTarget()//没有路线或走不过去，直接恢复巡逻，避免卡死
    {
        stateMachine.ChangeState(enemy.moveState);
    }
}
