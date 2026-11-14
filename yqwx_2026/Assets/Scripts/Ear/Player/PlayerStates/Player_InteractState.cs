using UnityEngine;

//交互（推拉箱子）：按住 F 期间保持抓取，推/拉一格的动作由 PlayerGridMove 播完。
//这个状态里可以移动（推拉本身就是移动），但不能攻击 —— 与攻击互斥。
public class Player_InteractState : PlayerState
{
    public Player_InteractState(PlayerBrain brain, StateMachine stateMachine, string animBoolName)
        : base(brain, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        //松开 F，或者箱子没了（被抓走/销毁）→ 立刻释放并回待机
        if (!brain.grabHeld || !brain.motor.IsGrabbing)
        {
            stateMachine.ChangeState(brain.idleState);
            return;
        }

        if (brain.motor.IsMoving)//推拉动作还没播完
            return;

        if (brain.moveInput != Vector2Int.zero)
        {
            //注意：这里不改朝向。朝向前方推、反方向拉，全靠进入时锁定的 Facing 判断。
            brain.motor.TryPushOrPull(brain.moveInput);
        }
    }

    public override void Exit()
    {
        brain.motor.ReleaseGrab();
        base.Exit();
    }
}
