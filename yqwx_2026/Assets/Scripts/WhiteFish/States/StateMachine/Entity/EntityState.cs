using UnityEngine;

//状态基类：提供生命周期，计时器，动画bool开关和动画事件标记
public abstract class EntityState
{
    protected readonly StateMachine stateMachine;//状态机实例
    protected readonly string animBoolName;//动画bool名称
    protected readonly Animator anim;//动画组件
    protected readonly string animPlayName;//直接播放的动画名，null 表示走 bool 参数体系

    protected float stateTimer;//状态计时器
    protected bool triggerCalled;//动画事件标记

    public EntityState(StateMachine stateMachine, string animBoolName = null, Animator anim = null, string animPlayName = null)
    {
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
        this.anim = anim;
        this.animPlayName = animPlayName;
    }

    public virtual void Enter()//进入新状态：复位动画事件标记，打开对应的动画 bool。
    {
        triggerCalled = false;
        SetAnimBool(true);
        PlayAnim();
    }

    public virtual void Update()//每帧更新：推进计时器并同步动画参数。
    {
        stateTimer -= Time.deltaTime;
        UpdateAnimationParameters();
    }

    public virtual void Exit()//退出状态：关闭对应的动画bool
    {
        SetAnimBool(false);
    }

    public virtual void UpdateAnimationParameters()//每帧同步动画参数的钩子，供子类覆写
    {
        
    }

    public void AnimationTrigger() => triggerCalled = true;//由Animation Event经MonoBehaviour转发调用，标记动画关键帧已到达

    public void ResetTimer() => stateTimer = 0f;

    private void SetAnimBool(bool value)
    {
        if (anim == null || string.IsNullOrEmpty(animBoolName))
            return;

        anim.SetBool(animBoolName, value);
    }

    //动画名解析钩子：子类可以按朝向等情况把基名换成别的 clip
    protected virtual string ResolveAnimName(string name) => name;

    private void PlayAnim()//按名字直接播放，用于没有 bool 参数的 Animator Controller
    {
        if (anim == null || string.IsNullOrEmpty(animPlayName))
            return;

        anim.Play(ResolveAnimName(animPlayName));
    }
}
