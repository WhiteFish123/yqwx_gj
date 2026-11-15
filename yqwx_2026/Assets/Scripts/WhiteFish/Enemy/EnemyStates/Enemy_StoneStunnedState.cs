using UnityEngine;

//石像鬼撞墙后的晕眩：原地停一段时间后回待机
public class Enemy_StoneStunnedState : EnemyState
{
    private readonly Enemy_Stone stone;//晕眩时长在子类上
    private readonly string stunAnimName;//晕眩动画名

    public Enemy_StoneStunnedState(Enemy_Stone stone, StateMachine stateMachine, string stunAnimName)
        : base(stone, stateMachine, null)//不参与 bool 参数体系
    {
        this.stone = stone;
        this.stunAnimName = stunAnimName;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = stone.stunTime;

        if (enemy.rb != null)
            enemy.rb.linearVelocity = Vector2.zero;

        if (anim != null && !string.IsNullOrEmpty(stunAnimName))
            anim.Play(stunAnimName);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer <= 0f)//晕眩结束，走回原位
            stone.GoHome();
    }
}
