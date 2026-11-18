using UnityEngine;

//石像鬼撞墙后的晕眩：原地停一段时间后回待机
public class Enemy_StoneStunnedState : EnemyState
{
    private readonly Enemy_Stone stone;//晕眩时长在子类上
    private readonly string stunAnimName;//晕眩动画名
    private bool goHomeRequested;//已发出回原位请求，避免每帧重复调用

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
        goHomeRequested = false;

        if (enemy.rb != null)
            enemy.rb.linearVelocity = Vector2.zero;

        if (anim != null && !string.IsNullOrEmpty(stunAnimName))
            anim.Play(ResolveAnimName(stunAnimName));
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer <= 0f && !goHomeRequested)//晕眩结束：只请求一次，避免切状态失败时每帧重试
        {
            goHomeRequested = true;
            stone.GoHome();
        }
    }
}
