using UnityEngine;

//移动：一格一格地走。跨格途中不重新决策，走完再看输入。
public class Player_MoveState : PlayerState
{
    public Player_MoveState(PlayerBrain brain, StateMachine stateMachine, string animBoolName)
        : base(brain, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if (brain.TryEnterAttack())//移动中也能开枪（设计上"边跑边打"是允许的）
            return;

        if (brain.TryEnterInteract())//按住 F 抓到箱子 → 转交互
            return;

        if (brain.motor.IsMoving)//正在跨格，等这一步走完
            return;

        if (brain.moveInput == Vector2Int.zero)//没有新的方向输入 → 停下
        {
            stateMachine.ChangeState(brain.idleState);
            return;
        }

        brain.motor.SetFacing(brain.moveInput);
        brain.motor.TryWalk(brain.moveInput);
    }
}
