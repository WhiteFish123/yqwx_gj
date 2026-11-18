using UnityEngine;

//石像鬼的蓄力与冲撞：原地定格抖动蓄力，然后朝锁定方向逐格冲到�?
public class Enemy_StoneChargeState : EnemyState
{
    private enum Phase
    {
        Charge,//原地蓄力
        Dash//逐格冲撞
    }

    private readonly Enemy_Stone stone;//石像鬼特有的参数在子类上

    private Phase phase;//当前阶段
    private float chargeTimer;//蓄力剩余时间
    private Vector3 baseCenter;//蓄力期间的基准位置（所在格中心�?

    private Vector2Int direction;//锁定的冲撞方�?
    private Vector3 stepFrom;//本步起点
    private Vector3 stepTo;//本步终点
    private float progress;//本步进度
    private bool isStepping;//是否正在跨格

    private int frameIndex;//当前冲刺帧下�?
    private float frameTimer;//当前帧已播放时间
    private bool frameLooping;//首轮是否已播�?

    public Enemy_StoneChargeState(Enemy_Stone stone, StateMachine stateMachine)
        : base(stone, stateMachine, null)//不参�?bool 参数体系
    {
        this.stone = stone;
    }

    public override void Enter()
    {
        base.Enter();

        phase = Phase.Charge;
        chargeTimer = stone.chargeTime;
        direction = enemy.facing;//方向锁定，蓄力期间玩家走开也不�?

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
            anim.enabled = false;//接管 sprite，蓄力期间定格在 holdFrame

        if (enemy.spriteRenderer != null && stone.CurrentHoldFrame != null)
            enemy.spriteRenderer.sprite = stone.CurrentHoldFrame;
    }

    public override void Exit()
    {
        if (phase == Phase.Charge)
            enemy.transform.position = baseCenter;//charge shakes off the cell center, snap back

        isStepping = false;
        progress = 0f;

        if (anim != null)
            anim.enabled = true;//交还�?Animator，保证后续状态动画正�?

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

        UpdateDash();
    }

    private void UpdateCharge()//原地抖动等蓄力结�?
    {
        chargeTimer -= Time.deltaTime;

        if (chargeTimer <= 0f)//蓄力结束：停止抖动、回归格中心、开�?
        {
            enemy.transform.position = baseCenter;
            phase = Phase.Dash;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame();//立刻显示冲刺动画第一�?
            return;
        }

        float offset = Mathf.Sin(Time.time * stone.shakeFrequency * Mathf.PI * 2f) * stone.shakeAmplitude;
        enemy.transform.position = baseCenter + new Vector3(offset, 0f, 0f);
    }

    private void UpdateDash()//逐格冲刺，同时按帧率推进冲刺动画
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

        if (enemy.CellHasObstacle(next))//撞墙：晕�?
        {
            stateMachine.TryChangeState(stone.stunnedState, "StoneCharge(hit wall/cross floor)");
            return;
        }

        if (!enemy.SameFloor(current, next))//跨楼层：当作撞墙，不能从一楼冲到二�?
        {
            stateMachine.TryChangeState(stone.stunnedState, "StoneCharge(hit wall/cross floor)");
            return;
        }

        if (IsPlayerAt(next))//撞到玩家：造成伤害，走回原位，并短暂不再察觉，避免原地无限蓄力
        {
            HurtPlayer();
            stone.SuppressDetect(stone.detectCooldownAfterBump);
            stone.GoHome();
            return;
        }

        if (IsOtherEnemyAt(next))//撞到别的敌人：走回原�?
        {
            stone.GoHome();
            return;
        }

        stepFrom = enemy.CellToCenter(current);
        stepTo = enemy.CellToCenter(next);
        progress = 0f;
        isStepping = true;
    }

    private void AdvanceFrames()//按帧率推进冲刺动�?
    {
        Sprite[] frames = stone.CurrentAttackFrames;

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
        Sprite[] frames = stone.CurrentAttackFrames;

        if (frames == null || frames.Length == 0 || enemy.spriteRenderer == null)
            return;

        enemy.spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private int NextFrameIndex(int count)//首轮递增，之后在倒数第三帧与最后一帧之间来�?
    {
        int loopStart = Mathf.Max(0, count - 3);//倒数第三帧（5 帧时是索�?2�?
        int last = count - 1;//最后一帧（5 帧时是索�?4�?

        //no loop when facing down: play once and stop at the last frame
        if (enemy.facing == Vector2Int.down && !stone.loopDownAttackFrames)
        {
            if (frameIndex < last)
                return frameIndex + 1;

            return last;
        }

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

    private bool IsPlayerAt(Vector3Int cell)//����Ƿ�վ�ڸø�
    {
        return EnemyRegistry.IsOccupiedByPlayer(cell, enemy.WorldToCell);//cached player, no per-step Find
    }

    private bool IsOtherEnemyAt(Vector3Int cell)//�ø��Ƿ��������ŵĵ���ռ��
    {
        return EnemyRegistry.IsOccupiedByOther(enemy, cell);//registry, no array allocation per dash step
    }
}
