using UnityEngine;

//攻击：从按下 J 开始，到武器后摇（recovery）结束时结束。
//期间不能移动、不能抓箱子 —— 攻击与交互互斥。
public class Player_AttackState : PlayerState
{
    private float timer;//还剩多久结束（进入时 = 武器的后摇时长）

    public Player_AttackState(PlayerBrain brain, StateMachine stateMachine, string animBoolName)
        : base(brain, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        //真正"打出去"发生在这里：用一次武器，拿回这次的后摇时长。
        //返回 0 表示没打出去（冷却中 / 手上没东西），这里给个极小值让它下一帧就退出。
        float recovery = brain.hand != null ? brain.hand.UseEquipped() : 0f;
        timer = Mathf.Max(0.01f, recovery);
    }

    public override void Update()
    {
        base.Update();

        timer -= Time.deltaTime;

        if (timer <= 0f)//后摇结束
            stateMachine.ChangeState(brain.idleState);
    }
}
