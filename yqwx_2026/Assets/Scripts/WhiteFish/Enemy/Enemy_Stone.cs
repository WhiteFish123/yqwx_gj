using UnityEngine;

//石像鬼：不追逐玩家，正前方直线发现玩家后原地蓄力，然后沿该方向冲撞到�?
public class Enemy_Stone : Enemy
{
    [Header("Stone")]
    public Vector2Int initialFacing = Vector2Int.right;//出生时的朝向
    public int detectRange = 7;//正前方直线检测格�?
    public float chargeTime = 1f;//蓄力时长（原地不动）
    public float dashDuration = 0.12f;//冲撞时每格用�?
    public float stunTime = 2.5f;//撞墙后的晕眩时长
    public float detectCooldownAfterBump = 2.5f;//撞到玩家后多久不再察觉，须不小于 idleTime 才能走完待机转巡�?

    [Header("Charge Visual")]
    public Sprite holdFrame;//蓄力期间定格的那一�?
    public Sprite[] attackFrames;//冲刺动画帧，首轮按顺序播，之后在最后三帧间循环
    public float shakeAmplitude = 0.06f;//蓄力抖动幅度（格�?
    public float shakeFrequency = 25f;//蓄力抖动频率（Hz�?
    public float attackFrameRate = 12f;//冲刺动画帧率

    [Header("正面（朝下）")]
    public Sprite holdFrameDown;//朝下蓄力时定格的那一�?
    public Sprite[] attackFramesDown;//朝下冲刺的帧序列
    public bool loopDownAttackFrames = false;//朝下冲刺时是否在最后三帧间循环（默认不循环�?

    //当前朝向该用哪一帧定�?
    public Sprite CurrentHoldFrame =>
        facing == Vector2Int.down && holdFrameDown != null ? holdFrameDown : holdFrame;

    //当前朝向该用哪一组冲刺帧
    public Sprite[] CurrentAttackFrames =>
        facing == Vector2Int.down && attackFramesDown != null && attackFramesDown.Length > 0
            ? attackFramesDown
            : attackFrames;

    public Enemy_StoneChargeState chargeState;//蓄力 + 冲撞
    public Enemy_StoneStunnedState stunnedState;//撞墙晕眩
    public Enemy_StoneReturnState stoneReturnState;//走回原位

    public Vector3Int homeCell { get; private set; }//出生格子，冲刺后回到这里

    private float nextDetectTime;//下次允许察觉的时间点

    protected override void Awake()//创建自身所需状�?
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, null, "Stone_Idle");
        moveState = new Enemy_MoveState(this, stateMachine, null, "Stone_Move");
        returnState = new Enemy_ReturnState(this, stateMachine, null, "Stone_Move");
        investigateState = new Enemy_StoneInvestigateState(this, stateMachine, null, "Stone_Move");
        chargeState = new Enemy_StoneChargeState(this, stateMachine);
        stunnedState = new Enemy_StoneStunnedState(this, stateMachine, "Stone_Stunned");
        stoneReturnState = new Enemy_StoneReturnState(this, stateMachine, null, "Stone_Move");
        deadState = new Enemy_DeadState(this, stateMachine, "Stone_Dead");

        chaseState = null;//石像鬼不追�?
    }

    protected override void Start()//记录原位并进入待机（石像鬼不巡逻，不构建巡逻路线）
    {
        base.Start();

        homeCell = WorldToCell(transform.position);//冲刺结束后回到这�?

        SetFacing(initialFacing);//出生朝向

        stateMachine.Initialize(idleState);
    }

    //动画名按朝向解析：朝下时待机/移动/晕眩换成正面�?clip
    public override string ResolveAnimName(string baseName)
    {
        if (facing != Vector2Int.down)
            return baseName;

        if (baseName == "Stone_Idle" || baseName == "Stone_Move")
            return "Stone_Idle_Down";

        if (baseName == "Stone_Stunned")
            return "Stone_Stunned_Down";

        return baseName;
    }

    //冲刺或晕眩结束后调用：已在原位就待机，否则走回去
    public void GoHome()
    {
        if (WorldToCell(transform.position) == homeCell)
        {
            SetFacing(initialFacing);//已在原位，朝向直接恢复成出生朝向
            stateMachine.TryChangeState(idleState, "Enemy_Stone.GoHome(idle)");
        }
        else
        {
            stateMachine.TryChangeState(stoneReturnState, "Enemy_Stone.GoHome(return)");
        }
    }

    public override bool CanDetectPlayer()//正前�?1 格宽 × detectRange �?
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
        return StoneLineOfSight.IsVisibleInLine(grid, obstacleMask, origin, facing, detectRange, target, secondFloorTilemap);
    }

    public override EnemyState GetAlertState()//察觉后进入蓄力，而不是追�?
    {
        return chargeState;
    }

    public void SuppressDetect(float duration)//撞到玩家后暂时不再察�?
    {
        nextDetectTime = Time.time + duration;
    }

    public override bool IsBusy()//蓄力 / 冲撞 / 晕眩期间不响应噪�?
    {
        EntityState current = stateMachine.currentState;
        return current == chargeState || current == stunnedState;
    }
}
