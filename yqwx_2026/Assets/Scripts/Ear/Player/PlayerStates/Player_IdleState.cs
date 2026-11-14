using UnityEngine;

//待机：没有输入时停在原地。
//任何输入优先变成 攻击 / 交互，其次才是移动。
public class Player_IdleState : PlayerState
{
    public Player_IdleState(PlayerBrain brain, StateMachine stateMachine, string animBoolName)
        : base(brain, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if (brain.TryEnterAttack())//按 J 且有可用武器
            return;

        if (brain.TryEnterInteract())//按住 F 且前方有箱子
            return;

        if (brain.moveInput == Vector2Int.zero)
            return;

        //★ 关键：在切换状态的"同一帧"就把这一步走掉。
        //  只改状态、等下一帧让 MoveState 去走是不行的 —— 那一帧输入已经没了，
        //  人物会表现为"按了方向键但纹丝不动"。
        brain.motor.SetFacing(brain.moveInput);
        bool walked = brain.motor.TryWalk(brain.moveInput);

        if (walked)
            stateMachine.ChangeState(brain.moveState);//迈出去了才进移动状态
    }
}
