using System;

//通用状态机：持有当前状态，负责进入、退出与每帧更新
public class StateMachine
{
    public EntityState currentState { get; private set; }
    public bool canChangeState { get; private set; }
    public event Action<EntityState> OnStateChanged;//状态切换事�?

    public void Initialize(EntityState startState)
    {
        if (startState == null)
            throw new ArgumentNullException(nameof(startState));

        if (currentState != null)
            throw new InvalidOperationException("StateMachine 已经初始化过，无法调用Initialize");

        canChangeState = true;//初始化时允许切换状�?
        currentState = startState;//设置起始状�?
        currentState.Enter();//进入起始状�?
    }

    //判断是否可以切换状�?
    public bool ChangeState(EntityState newState)
    {
        if (canChangeState == false || newState == null || newState == currentState)//锁定，目标为null或与当前状态相同时返回false
            return false;

        currentState.Exit();//退出当前状�?
        currentState = newState;//设置新状�?
        currentState.Enter();//进入新状�?
        OnStateChanged?.Invoke(currentState);
        return true;
    }


    //ChangeState with diagnostics: identical state is a normal no-op, but a null
    //target or a locked machine means the caller is stuck, so say so loudly.
    public bool TryChangeState(EntityState newState, string reason = null)
    {
        if (newState == currentState)
            return false;//already there, not an error

        if (ChangeState(newState))
            return true;

        UnityEngine.Debug.LogError("[StateMachine] �л�״̬ʧ��(" + (reason ?? "δ˵��") + ") Ŀ��="
            + (newState == null ? "null" : newState.GetType().Name)
            + " ��ǰ=" + (currentState == null ? "null" : currentState.GetType().Name)
            + " ����=" + !canChangeState);
        return false;
    }

    public void UpdateActiveState()//驱动当前状态每帧更�?
    {
        currentState.Update();
    }
    
    //关闭状态机，之后不再允许切换（用于死亡等终态）
    public void SwitchOffStateMachine() => canChangeState = false;
}
