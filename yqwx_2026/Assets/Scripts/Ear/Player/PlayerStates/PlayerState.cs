//主角状态基类：把 PlayerBrain 注入进来，并把动画 bool 交给 EntityState 统一处理。
public class PlayerState : EntityState
{
    protected readonly PlayerBrain brain;

    public PlayerState(PlayerBrain brain, StateMachine stateMachine, string animBoolName)
        : base(stateMachine, animBoolName, brain.anim)
    {
        this.brain = brain;
    }
}
