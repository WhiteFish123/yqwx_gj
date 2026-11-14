using System;

//通用状态机：持有当前状态，负责进入、退出与每帧更新
public class StateMachine
{
    public EntityState currentState { get; private set; }
    public bool canChangeState { get; private set; }
    public event Action<EntityState> OnStateChanged;//状态切换事件

    public void Initialize(EntityState startState)
    {
        if (startState == null)
            throw new ArgumentNullException(nameof(startState));

        if (currentState != null)
            throw new InvalidOperationException("StateMachine 已经初始化过，无法调用Initialize");

        canChangeState = true;//初始化时允许切换状态
        currentState = startState;//设置起始状态
        currentState.Enter();//进入起始状态
    }

    //判断是否可以切换状态
    public bool ChangeState(EntityState newState)
    {
        if (canChangeState == false || newState == null || newState == currentState)//锁定，目标为null或与当前状态相同时返回false
            return false;

        currentState.Exit();//退出当前状态
        currentState = newState;//设置新状态
        currentState.Enter();//进入新状态
        OnStateChanged?.Invoke(currentState);
        return true;
    }

    public void UpdateActiveState()//驱动当前状态每帧更新
    {
        currentState.Update();
    }
    
    //关闭状态机，之后不再允许切换（用于死亡等终态）
    public void SwitchOffStateMachine() => canChangeState = false;
}
