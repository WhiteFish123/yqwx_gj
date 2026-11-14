//史莱姆：创建自身所需状态，并在开始时进入待机
public class Enemy_Slime : Enemy
{
    protected override void Awake()//创建状态实例
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, "Idle");
    }

    protected override void Start()//进入初始状态
    {
        base.Start();

        stateMachine.Initialize(idleState);
    }
}
