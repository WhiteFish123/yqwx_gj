using UnityEngine;

//敌人死亡状态：关闭碰撞、播放死亡动画、锁死状态机并延迟销毁
public class Enemy_DeadState : EnemyState
{
    private readonly Collider2D col;//碰撞体（可能为空）
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

        if (enemy.rb != null)//停下脚步，防止尸体滑行
            enemy.rb.linearVelocity = Vector2.zero;

        if (col != null)
            col.enabled = false;

        if (anim != null)
            anim.Play(deadAnimName);

        stateMachine.SwitchOffStateMachine();
        enemy.DestroyGameObjectWithDelay(destroyDelay);
    }
}
