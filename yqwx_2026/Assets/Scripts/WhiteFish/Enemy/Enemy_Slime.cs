//史莱姆：创建自身所需状态，并在开始时进入待机
public class Enemy_Slime : Enemy
{
    protected override void Awake()//创建状态实例
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, "Idle");
        moveState = new Enemy_MoveState(this, stateMachine, "Move");
        chaseState = new Enemy_ChaseState(this, stateMachine, "Move");
        attackState = new Enemy_AttackState(this, stateMachine, "Slime_Attack", "Slime_Move");
        returnState = new Enemy_ReturnState(this, stateMachine, "Move");
        deadState = new Enemy_DeadState(this, stateMachine, "Slime_Dead", 2f);
    }

    protected override void Start()//构建巡逻路线并进入初始状态
    {
        base.Start();

        BuildPatrolPath();
        stateMachine.Initialize(idleState);
    }
}
