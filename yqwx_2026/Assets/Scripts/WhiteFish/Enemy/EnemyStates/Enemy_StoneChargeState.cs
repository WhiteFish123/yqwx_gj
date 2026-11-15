using UnityEngine;

//石像鬼的蓄力与冲撞：原地定格抖动蓄力，然后朝锁定方向逐格冲到底
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
    private Vector3 baseCenter;//蓄力期间的基准位置（所在格中心）

    private Vector2Int direction;//锁定的冲撞方向
    private Vector3 stepFrom;//本步起点
    private Vector3 stepTo;//本步终点
    private float progress;//本步进度
    private bool isStepping;//是否正在跨格

    private int frameIndex;//当前冲刺帧下标
    private float frameTimer;//当前帧已播放时间
    private bool frameLooping;//首轮是否已播完

    public Enemy_StoneChargeState(Enemy_Stone stone, StateMachine stateMachine)
        : base(stone, stateMachine, null)//不参与 bool 参数体系
    {
        this.stone = stone;
    }

    public override void Enter()
    {
        base.Enter();

        phase = Phase.Charge;
        chargeTimer = stone.chargeTime;
        direction = enemy.facing;//方向锁定，蓄力期间玩家走开也不改

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

        if (enemy.spriteRenderer != null && stone.holdFrame != null)
            enemy.spriteRenderer.sprite = stone.holdFrame;
    }

    public override void Exit()
    {
        isStepping = false;
        progress = 0f;

        if (anim != null)
            anim.enabled = true;//交还给 Animator，保证后续状态动画正常

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

    private void UpdateCharge()//原地抖动等蓄力结束
    {
        chargeTimer -= Time.deltaTime;

        if (chargeTimer <= 0f)//蓄力结束：停止抖动、回归格中心、开冲
        {
            enemy.transform.position = baseCenter;
            phase = Phase.Dash;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame();//立刻显示冲刺动画第一帧
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

        if (enemy.CellHasObstacle(next))//撞墙：晕眩
        {
            stateMachine.ChangeState(stone.stunnedState);
            return;
        }

        if (IsPlayerAt(next))//撞到玩家：造成伤害，走回原位，并短暂不再察觉，避免原地无限蓄力
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

    private void AdvanceFrames()//按帧率推进冲刺动画
    {
        Sprite[] frames = stone.attackFrames;

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
        Sprite[] frames = stone.attackFrames;

        if (frames == null || frames.Length == 0 || enemy.spriteRenderer == null)
            return;

        enemy.spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private int NextFrameIndex(int count)//首轮递增，之后在倒数第三帧与最后一帧之间来回
    {
        int loopStart = Mathf.Max(0, count - 3);//倒数第三帧（5 帧时是索引 2）
        int last = count - 1;//最后一帧（5 帧时是索引 4）

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
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null && enemy.WorldToCell(player.transform.position) == cell;
    }

    private bool IsOtherEnemyAt(Vector3Int cell)//该格是否被其他活着的敌人占着
    {
        foreach (Enemy other in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (other == null || other == enemy || other.isDead)
                continue;

            if (other.WorldToCell(other.transform.position) == cell)
                return true;
        }

        return false;
    }
}
