using UnityEngine;
//敌人死亡状态
public class Enemy_DeadState : EnemyState
{
    private readonly Collider2D col;//碰撞体
    private readonly string deadAnimName;//死亡动画名
    private readonly float destroyDelay;//销毁延迟

    public Enemy_DeadState(Enemy enemy, StateMachine stateMachine, string deadAnimName, float destroyDelay = 2f)
        : base(enemy, stateMachine, null)//死亡状态不参与动画bool体系
    {
        col = enemy.GetComponent<Collider2D>();
        this.deadAnimName = deadAnimName;
        this.destroyDelay = destroyDelay;
    }

    public override void Enter()//关碰撞、播死亡动画、锁死状态机、延迟销毁
    {
        base.Enter();

        if (col != null)
            col.enabled = false;

        if (anim != null)
            anim.Play(deadAnimName);

        stateMachine.SwitchOffStateMachine();
        enemy.DestroyGameObjectWithDelay(destroyDelay);
    }
}
