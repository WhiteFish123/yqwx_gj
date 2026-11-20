using UnityEngine;

//石像鬼激活：察觉玩家后原地播放 Alarm 激活帧，播完再停一下，然后交给蓄力抖动
public class Enemy_StoneActivateState : EnemyState
{
    private readonly Enemy_Stone stone;//激活帧与时长在子类上

    private float elapsed;//已经播了多久
    private float totalDuration;//整段激活时长
    private int frameIndex;//当前帧下标

    public Enemy_StoneActivateState(Enemy_Stone stone, StateMachine stateMachine)
        : base(stone, stateMachine, null)//不走 bool 参数体系
    {
        this.stone = stone;
    }

    public override void Enter()
    {
        base.Enter();

        elapsed = 0f;
        frameIndex = 0;

        if (enemy.rb != null)
            enemy.rb.linearVelocity = Vector2.zero;

        if (anim != null)
            anim.enabled = false;//接管 sprite，激活期间手动画帧

        Sprite[] frames = stone.CurrentAlarmFrames;
        float frameDuration = 1f / Mathf.Max(1f, stone.alarmFrameRate);
        int count = frames == null ? 0 : frames.Length;
        totalDuration = count * frameDuration + Mathf.Max(0f, stone.activateHoldTime);

        ApplyFrame();

        if (count == 0)//没配激活帧就直接进蓄力
        {
            stateMachine.TryChangeState(stone.chargeState, "StoneActivate(no frames)");
        }
    }

    public override void Exit()
    {
        if (anim != null)
            anim.enabled = true;//交还给 Animator

        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        Sprite[] frames = stone.CurrentAlarmFrames;
        if (frames == null || frames.Length == 0)
            return;

        elapsed += Time.deltaTime;

        float frameDuration = 1f / Mathf.Max(1f, stone.alarmFrameRate);
        int index = Mathf.FloorToInt(elapsed / frameDuration);
        if (index >= frames.Length)
            index = frames.Length - 1;

        if (index != frameIndex)
        {
            frameIndex = index;
            ApplyFrame();
        }

        if (elapsed >= totalDuration)//激活完成 → 蓄力抖动
        {
            stateMachine.TryChangeState(stone.chargeState, "StoneActivate(finished)");
        }
    }

    private void ApplyFrame()//把当前帧写到渲染器上
    {
        Sprite[] frames = stone.CurrentAlarmFrames;
        if (frames == null || frames.Length == 0 || enemy.spriteRenderer == null)
            return;

        enemy.spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }
}
