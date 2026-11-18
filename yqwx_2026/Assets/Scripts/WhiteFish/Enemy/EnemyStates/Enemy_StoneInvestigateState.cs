using UnityEngine;

//石像鬼调查声音：走到噪音格旁边查看，结束后走回原位（普通敌人是回待机再回巡逻线）
public class Enemy_StoneInvestigateState : Enemy_InvestigateState
{
    private readonly Enemy_Stone stone;//原位与返回逻辑在子类上

    public Enemy_StoneInvestigateState(Enemy_Stone stone, StateMachine stateMachine, string animBoolName, string animPlayName = null)
        : base(stone, stateMachine, animBoolName, animPlayName)
    {
        this.stone = stone;
    }

    protected override void OnArrived()//到了噪音旁边；若箱子还在烧，父类会让我们原地守着
    {
        base.OnArrived();
    }

    protected override void OnWatchFinished()//守候结束（箱子熄灭）→ 走回原位
    {
        stone.GoHome();
    }

    protected override void OnNoTarget()//走不过去，回原位
    {
        stone.GoHome();
    }
}
