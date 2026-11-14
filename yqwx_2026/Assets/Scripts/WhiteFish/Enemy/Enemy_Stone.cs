using UnityEngine;

//石像鬼：不追逐玩家，正前方直线发现玩家后原地蓄力，然后沿该方向冲撞到底
public class Enemy_Stone : Enemy
{
    [Header("Stone")]
    public int detectRange = 7;//正前方直线检测格数
    public float chargeTime = 1f;//蓄力时长（原地不动）
    public float dashDuration = 0.12f;//冲撞时每格用时
    public float stunTime = 2.5f;//撞墙后的晕眩时长
    public float detectCooldownAfterBump = 2.5f;//撞到玩家后多久不再察觉，须不小于 idleTime 才能走完待机转巡逻

    [Header("Charge Visual")]
    public Sprite holdFrame;//蓄力期间定格的那一帧
    public Sprite[] attackFrames;//冲刺动画帧，首轮按顺序播，之后在最后三帧间循环
    public float shakeAmplitude = 0.06f;//蓄力抖动幅度（格）
    public float shakeFrequency = 25f;//蓄力抖动频率（Hz）
    public float attackFrameRate = 12f;//冲刺动画帧率

    public Enemy_StoneChargeState chargeState;//蓄力 + 冲撞
    public Enemy_StoneStunnedState stunnedState;//撞墙晕眩

    private float nextDetectTime;//下次允许察觉的时间点

    protected override void Awake()//创建自身所需状态
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, null, "Stone_Idle");
        moveState = new Enemy_MoveState(this, stateMachine, null, "Stone_Move");
        returnState = new Enemy_ReturnState(this, stateMachine, null, "Stone_Move");
        investigateState = new Enemy_InvestigateState(this, stateMachine, null, "Stone_Move");
        chargeState = new Enemy_StoneChargeState(this, stateMachine);
        stunnedState = new Enemy_StoneStunnedState(this, stateMachine, "Stone_Stunned");
        deadState = new Enemy_DeadState(this, stateMachine, "Stone_Dead");

        chaseState = null;//石像鬼不追逐
    }

    protected override void Start()//构建巡逻路线并进入初始状态
    {
        base.Start();

        BuildPatrolPath();
        stateMachine.Initialize(idleState);
    }

    public override bool CanDetectPlayer()//正前方 1 格宽 × detectRange 格
    {
        if (Time.time < nextDetectTime)//撞到玩家后的短暂冷却
            return false;

        if (grid == null)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        Vector3Int origin = WorldToCell(transform.position);
        Vector3Int target = WorldToCell(player.transform.position);
        return StoneLineOfSight.IsVisibleInLine(grid, obstacleMask, origin, facing, detectRange, target);
    }

    public override EnemyState GetAlertState()//察觉后进入蓄力，而不是追逐
    {
        return chargeState;
    }

    public void SuppressDetect(float duration)//撞到玩家后暂时不再察觉
    {
        nextDetectTime = Time.time + duration;
    }

    public override bool IsBusy()//蓄力 / 冲撞 / 晕眩期间不响应噪音
    {
        EntityState current = stateMachine.currentState;
        return current == chargeState || current == stunnedState;
    }
}
