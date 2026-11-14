using UnityEngine;

//待机状态：发现玩家就追；待机结束后按位置决定回巡逻还是先走回路线
public class Enemy_IdleState : EnemyState
{
    public Enemy_IdleState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemy.idleTime;

        if (enemy.rb != null)
            enemy.rb.linearVelocity = Vector2.zero;//待机必须静止，防止上一状态残留速度继续滑行
    }

    public override void Update()
    {
        base.Update();

        if (enemy.chaseState != null && enemy.CanSeePlayer())//待机时发现玩家
        {
            stateMachine.ChangeState(enemy.chaseState);
            return;
        }

        if (stateTimer >= 0 || enemy.patrolPoints == null)
            return;

        if (enemy.IsOnPatrolRoute() || enemy.returnState == null)
            stateMachine.ChangeState(enemy.moveState);//已在路线上，直接巡逻
        else
            stateMachine.ChangeState(enemy.returnState);//不在路线上，先走回去
    }
}
