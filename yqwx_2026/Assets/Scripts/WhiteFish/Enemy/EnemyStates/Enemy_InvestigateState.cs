using UnityEngine;

//调查声音：走�?听到声音的格�?旁边查看，途中看见玩家就转成追逐�?
//
// 为什么这么短：真�?怎么一格一格走过去、怎么绕开障碍、遇到玩�?别的敌人占格怎么让路"
// 全都写在父类 Enemy_PathMoveState 里了，子类只需要回答三个问题：
//   1) 目标格在�?    �?GetTargetCell()
//   2) 走多�?        �?StepDuration
//   3) 什么时候放�?  �?ShouldAbort() / OnNoTarget()
public class Enemy_InvestigateState : Enemy_PathMoveState
{
    private INoiseSource watchedSource;//到达后发现还在响的噪音源（如燃烧中的箱子�?
    private bool watching;//是否正在守着噪音源不�?

    public Enemy_InvestigateState(Enemy enemy, StateMachine stateMachine, string animBoolName, string animPlayName = null)
        : base(enemy, stateMachine, animBoolName, animPlayName)
    {
    }

    //走一格用时：比巡逻快、比追逐慢（数值在 Enemy �?Investigate Duration 上调�?
    protected override float StepDuration => enemy.investigateDuration;

    public override void Exit()
    {
        enemy.ClearHeardCell();//离开调查状态就不再记着这个位置
        watchedSource = null;
        watching = false;
        base.Exit();
    }

    public override void Update()
    {
        //守着噪音源：原地不动，直到噪音停（箱子熄灭）才继续往下走
        if (watching)
        {
            if (watchedSource != null && watchedSource.IsAlive && watchedSource.IsNoiseActive)
                return;//还在响，继续守着

            watching = false;
            watchedSource = null;
            OnWatchFinished();
            return;
        }

        base.Update();
    }

    //守候结束（噪音停了）时的处理，子类可覆�?
    protected virtual void OnWatchFinished()
    {
        if (enemy.idleState != null)
            stateMachine.ChangeState(enemy.idleState);
    }

    //在噪音格上找一个仍在发声的噪音�?
    private static INoiseSource FindNoiseSource(Grid grid, Vector3Int cell)
    {
        if (grid == null)
            return null;

        Vector3 size = grid.cellSize * 0.8f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(grid.GetCellCenterWorld(cell), size, 0f);

        foreach (Collider2D hit in hits)
        {
            INoiseSource source = hit.GetComponent<INoiseSource>();
            if (source != null)
                return source;
        }

        return null;
    }

    protected override bool ShouldAbort()//中途看见玩�?�?立刻转追�?
    {
        EnemyState alert = enemy.GetAlertState();
        if (alert != null && enemy.CanDetectPlayer())
        {
            stateMachine.ChangeState(alert);
            return true;
        }

        return false;
    }

    protected override Vector3Int? GetTargetCell()//目标�?噪音格旁边的一�?，不要站到噪音格�?
    {
        if (enemy.heardCell == null)
            return null;

        Vector3Int start = enemy.WorldToCell(enemy.transform.position);
        return enemy.pathfinder.FindAdjacentGoal(start, enemy.heardCell.Value);
    }

    protected override void OnArrived()//到了 �?如果噪音源还在响就守着，否则回待机
    {
        if (watchedSource == null && enemy.heardCell != null)
            watchedSource = FindNoiseSource(enemy.grid, enemy.heardCell.Value);

            if (watchedSource != null && watchedSource.IsAlive && watchedSource.IsNoiseActive)
        {
            watching = true;//原地待着，等箱子烧完
            return;
        }

        watchedSource = null;
        OnWatchFinished();
    }

    protected override void OnNoTarget()//走不过去（被墙挡�?/ 没有路径）→ 放弃
    {
        if (enemy.idleState != null)
            stateMachine.ChangeState(enemy.idleState);
    }
}
