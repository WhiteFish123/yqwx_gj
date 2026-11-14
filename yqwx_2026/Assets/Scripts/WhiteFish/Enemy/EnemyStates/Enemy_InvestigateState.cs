using UnityEngine;

//调查声音：走到"听到声音的格子"旁边查看，途中看见玩家就转成追逐。
//
// 为什么这么短：真正"怎么一格一格走过去、怎么绕开障碍、遇到玩家/别的敌人占格怎么让路"
// 全都写在父类 Enemy_PathMoveState 里了，子类只需要回答三个问题：
//   1) 目标格在哪     → GetTargetCell()
//   2) 走多快         → StepDuration
//   3) 什么时候放弃   → ShouldAbort() / OnNoTarget()
public class Enemy_InvestigateState : Enemy_PathMoveState
{
    public Enemy_InvestigateState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }

    //走一格用时：比巡逻快、比追逐慢（数值在 Enemy 的 Investigate Duration 上调）
    protected override float StepDuration => enemy.investigateDuration;

    public override void Exit()
    {
        enemy.ClearHeardCell();//离开调查状态就不再记着这个位置
        base.Exit();
    }

    protected override bool ShouldAbort()//中途看见玩家 → 立刻转追逐
    {
        if (enemy.chaseState != null && enemy.CanSeePlayer())
        {
            stateMachine.ChangeState(enemy.chaseState);
            return true;
        }

        return false;
    }

    protected override Vector3Int? GetTargetCell()//目标是"噪音格旁边的一格"，不要站到噪音格上
    {
        if (enemy.heardCell == null)
            return null;

        Vector3Int start = enemy.WorldToCell(enemy.transform.position);
        return enemy.pathfinder.FindAdjacentGoal(start, enemy.heardCell.Value);
    }

    protected override void OnArrived()//到了 → 回待机（待机计时结束会自动接回巡逻）
    {
        if (enemy.idleState != null)
            stateMachine.ChangeState(enemy.idleState);
    }

    protected override void OnNoTarget()//走不过去（被墙挡住 / 没有路径）→ 放弃
    {
        if (enemy.idleState != null)
            stateMachine.ChangeState(enemy.idleState);
    }
}
