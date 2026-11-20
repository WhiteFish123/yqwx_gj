using UnityEngine;

//石像鬼的蓄力与冲撞：原地定格抖动蓄力，抖动结束归位停顿一下，然后朝锁定方向逐格冲撞
public class Enemy_StoneChargeState : EnemyState
{
    private enum Phase
    {
        Charge,//原地蓄力抖动
        Settle,//抖动结束、归位停顿
        Dash//逐格冲撞
    }

    private readonly Enemy_Stone stone;//石像鬼特有的参数在子类上

    private Phase phase;//当前阶段
    private float chargeTimer;//蓄力剩余时间
    private float settleTimer;//归位停顿剩余时间
    private Vector3 baseCenter;//蓄力期间的基准位置（所在格中心）

    private Vector2Int direction;//锁定的冲撞方向
    private Vector3 stepFrom;//本步起点
    private Vector3 stepTo;//本步终点
    private float progress;//本步进度
    private bool isStepping;//是否正在跨格

    private int frameIndex;//当前冲撞帧下标
    private float frameTimer;//当前帧已播放时间
    private bool frameLooping;//首轮是否已播完

    public Enemy_StoneChargeState(Enemy_Stone stone, StateMachine stateMachine)
        : base(stone, stateMachine, null)//不参 bool 参数体系
    {
        this.stone = stone;
    }

    public override void Enter()
    {
        base.Enter();

        phase = Phase.Charge;
        chargeTimer = stone.chargeTime;
        settleTimer = 0f;
        direction = enemy.facing;//方向锁定，蓄力期间玩家走开也不变

        isStepping = false;
        progress = 0f;

        frameIndex = 0;
        frameTimer = 0f;
        frameLooping = false;

        if (enemy.rb != null)
            enemy.rb.linearVelocity = Vector2.zero;

        baseCenter = enemy.CellToCenter(enemy.WorldToCell(enemy.transform.position));
        enemy.transform.position = baseCenter;//吸附到格中心

        if (anim != null)
            anim.enabled = false;//接管 sprite，蓄力期间定格

        ApplyHoldFrame();
    }

    public override void Exit()
    {
        if (phase == Phase.Charge || phase == Phase.Settle)
            enemy.transform.position = baseCenter;//抖动/归位阶段会偏离格心，退出时归位

        isStepping = false;
        progress = 0f;

        if (anim != null)
            anim.enabled = true;//交还 Animator

        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (phase == Phase.Charge)
        {
            UpdateCharge();
            return;
        }

        if (phase == Phase.Settle)
        {
            UpdateSettle();
            return;
        }

        UpdateDash();
    }

    private void ApplyHoldFrame()//蓄力时显示的定格帧
    {
        if (enemy.spriteRenderer != null && stone.CurrentHoldFrame != null)
            enemy.spriteRenderer.sprite = stone.CurrentHoldFrame;
    }

    private void UpdateCharge()//原地抖动等蓄力结束
    {
        chargeTimer -= Time.deltaTime;

        if (chargeTimer <= 0f)//蓄力结束：停止抖动、归位、进入停顿
        {
            enemy.transform.position = baseCenter;
            phase = Phase.Settle;
            settleTimer = Mathf.Max(0f, stone.settleTime);
            ApplyHoldFrame();
            return;
        }

        float offset = Mathf.Sin(Time.time * stone.shakeFrequency * Mathf.PI * 2f) * stone.shakeAmplitude;
        enemy.transform.position = baseCenter + new Vector3(offset, 0f, 0f);
    }

    private void UpdateSettle()//归位停顿，之后开始冲撞
    {
        settleTimer -= Time.deltaTime;

        if (settleTimer > 0f)
            return;

        phase = Phase.Dash;
        frameIndex = 0;
        frameTimer = 0f;
        frameLooping = false;
        ApplyFrame();//立刻显示冲撞动画第一帧
    }

    private void UpdateDash()//逐格冲撞，同时按帧率推进冲撞动画
    {
        AdvanceFrames();

        if (isStepping)
        {
            progress += Time.deltaTime / Mathf.Max(0.01f, stone.dashDuration);
            enemy.transform.position = Vector3.Lerp(stepFrom, stepTo, Mathf.Clamp01(progress));

            if (progress >= 1f)
            {
                enemy.transform.position = stepTo;
                isStepping = false;
                progress = 0f;
            }

            return;
        }

        Vector3Int current = enemy.WorldToCell(enemy.transform.position);
        Vector3Int next = current + new Vector3Int(direction.x, direction.y, 0);

        if (enemy.CellHasObstacle(next))//撞墙：眩晕
        {
            stateMachine.TryChangeState(stone.stunnedState, "StoneCharge(hit wall/cross floor)");
            return;
        }

        if (!enemy.SameFloor(current, next))//跨楼层：当作撞墙
        {
            stateMachine.TryChangeState(stone.stunnedState, "StoneCharge(hit wall/cross floor)");
            return;
        }

        if (IsPlayerAt(next))//撞到玩家：造成伤害，走回原位，并短暂不再察觉
        {
            HurtPlayer();
            stone.SuppressDetect(stone.detectCooldownAfterBump);
            stone.GoHome();
            return;
        }

        if (IsOtherEnemyAt(next))//撞到别的敌人：走回原位
        {
            stone.GoHome();
            return;
        }

        stepFrom = enemy.CellToCenter(current);
        stepTo = enemy.CellToCenter(next);
        progress = 0f;
        isStepping = true;
    }

    private void AdvanceFrames()//按帧率推进冲撞动画
    {
        Sprite[] frames = stone.CurrentChargeFrames;

        if (frames == null || frames.Length == 0 || enemy.spriteRenderer == null)
            return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, stone.attackFrameRate);

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = NextFrameIndex(frames.Length);
        }

        ApplyFrame();
    }

    private void ApplyFrame()//把当前帧写到渲染器上
    {
        Sprite[] frames = stone.CurrentChargeFrames;

        if (frames == null || frames.Length == 0 || enemy.spriteRenderer == null)
            return;

        enemy.spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private int NextFrameIndex(int count)//整段播完一遍后，只在最后两帧之间来回
    {
        int loopStart = Mathf.Max(0, count - 2);//倒数第二帧（5 帧时是第 4 帧）
        int last = count - 1;//最后一帧（5 帧时是第 5 帧）

        if (frameLooping)
            return frameIndex == loopStart ? last : loopStart;

        if (frameIndex < last)
            return frameIndex + 1;

        frameLooping = true;
        return loopStart;
    }

    //撞到玩家：扣一滴血
    private void HurtPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        IEnemyAttackTarget target = player.GetComponent<IEnemyAttackTarget>();
        if (target != null)
            target.TakeEnemyHit(1);
    }

    private bool IsPlayerAt(Vector3Int cell)//玩家是否站在该格
    {
        return EnemyRegistry.IsOccupiedByPlayer(cell, enemy.WorldToCell);
    }

    private bool IsOtherEnemyAt(Vector3Int cell)//该格是否被其他活着的敌人占着
    {
        return EnemyRegistry.IsOccupiedByOther(enemy, cell);
    }
}
