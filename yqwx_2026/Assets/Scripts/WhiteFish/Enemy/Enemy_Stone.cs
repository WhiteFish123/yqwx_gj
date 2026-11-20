using UnityEngine;

//石像鬼：不追逐玩家，正前方直线发现玩家后先原地激活（Alarm），
//激活结束开始蓄力抖动，抖动结束归位，然后沿该方向逐格冲撞。
public class Enemy_Stone : Enemy
{
    //Scene 里可选的朝向（比 Vector2Int 直观）
    public enum StoneFacing
    {
        Down,   //朝下（正面，玩家看到脸）
        Up,     //朝上（背面，玩家看到后脑）
        Left,
        Right
    }

    //一套朝向对应的美术：激活 / 冲撞 / 静止帧
    [System.Serializable]
    public class StoneDirectionArt
    {
        public Sprite[] alarm;//激活帧（Alarm 文件夹）
        public Sprite[] charge;//冲撞帧（Charge 文件夹）
        public Sprite idle;//静止帧（蓄力抖动时显示；留空则用 charge 第一帧）
    }

    [Header("Stone")]
    [Tooltip("石像鬼的初始朝向，Scene 里直接选")]
    public StoneFacing facingOption = StoneFacing.Right;
    public Vector2Int initialFacing = Vector2Int.right;//兼容旧数据，运行时会被 facingOption 覆盖
    public int detectRange = 7;//正前方直线检测格数
    public float chargeTime = 1f;//蓄力抖动时长
    public float settleTime = 0.15f;//抖动结束归位后，停顿多久再冲撞
    public float dashDuration = 0.12f;//冲撞时每格用时
    public float stunTime = 2.5f;//撞墙后的眩晕时长
    public float detectCooldownAfterBump = 2.5f;//撞到玩家后多久不再察觉

    [Header("Activate")]
    public float alarmFrameRate = 12f;//激活动画帧率
    public float activateHoldTime = 0.2f;//激活最后一帧再停一下

    [Header("Directional Art")]
    public StoneDirectionArt artDown;//朝下
    public StoneDirectionArt artUp;//朝上
    public StoneDirectionArt artSide;//左右共用（靠 flipX）
    public float shakeAmplitude = 0.06f;//蓄力抖动幅度（格）
    public float shakeFrequency = 25f;//蓄力抖动频率（Hz）
    public float attackFrameRate = 12f;//冲撞动画帧率

    public Enemy_StoneActivateState activateState;//激活
    public Enemy_StoneChargeState chargeState;//蓄力 + 冲撞
    public Enemy_StoneStunnedState stunnedState;//撞墙眩晕
    public Enemy_StoneReturnState stoneReturnState;//走回原位

    public Vector3Int homeCell { get; private set; }//出生格子，冲撞后回到这里

    private float nextDetectTime;//下次允许察觉的时间点

    //当前朝向对应的那一套美术
    public StoneDirectionArt CurrentArt
    {
        get
        {
            if (facing == Vector2Int.up)
                return artUp;
            if (facing == Vector2Int.down)
                return artDown;
            return artSide;
        }
    }

    public Sprite[] CurrentAlarmFrames
    {
        get
        {
            StoneDirectionArt art = CurrentArt;
            return art != null && art.alarm != null && art.alarm.Length > 0 ? art.alarm : null;
        }
    }

    public Sprite[] CurrentChargeFrames
    {
        get
        {
            StoneDirectionArt art = CurrentArt;
            if (art != null && art.charge != null && art.charge.Length > 0)
                return art.charge;

            //没配冲撞帧就退回激活帧
            return CurrentAlarmFrames;
        }
    }

    //蓄力抖动时显示的定格帧
    public Sprite CurrentHoldFrame
    {
        get
        {
            StoneDirectionArt art = CurrentArt;
            if (art != null && art.idle != null)
                return art.idle;

            Sprite[] frames = CurrentChargeFrames;
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }
    }

    //兼容旧字段名
    public Sprite[] CurrentAttackFrames => CurrentChargeFrames;

    protected override void Awake()//创建自身所需状态
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, null, "Stone_Idle");
        moveState = new Enemy_MoveState(this, stateMachine, null, "Stone_Move");
        returnState = new Enemy_ReturnState(this, stateMachine, null, "Stone_Move");
        investigateState = new Enemy_StoneInvestigateState(this, stateMachine, null, "Stone_Move");
        activateState = new Enemy_StoneActivateState(this, stateMachine);
        chargeState = new Enemy_StoneChargeState(this, stateMachine);
        stunnedState = new Enemy_StoneStunnedState(this, stateMachine, "Stone_Stunned");
        stoneReturnState = new Enemy_StoneReturnState(this, stateMachine, null, "Stone_Move");
        deadState = new Enemy_DeadState(this, stateMachine, "Stone_Dead");

        chaseState = null;//石像鬼不追逐
    }

    protected override void Start()//记录原位并进入待机
    {
        base.Start();

        homeCell = WorldToCell(transform.position);//冲撞结束后回到这里

        initialFacing = FacingVector(facingOption);
        SetFacing(initialFacing);

        stateMachine.Initialize(idleState);
    }

    //枚举 → 方向向量
    public static Vector2Int FacingVector(StoneFacing option)
    {
        switch (option)
        {
            case StoneFacing.Up: return Vector2Int.up;
            case StoneFacing.Down: return Vector2Int.down;
            case StoneFacing.Left: return Vector2Int.left;
            default: return Vector2Int.right;
        }
    }

    //动画名按朝向解析
    public override string ResolveAnimName(string baseName)
    {
        string suffix = null;

        if (facing == Vector2Int.down)
            suffix = "_Down";
        else if (facing == Vector2Int.up)
            suffix = "_Up";

        if (suffix == null)
            return baseName;//左右用默认 clip

        if (baseName == "Stone_Idle" || baseName == "Stone_Move" || baseName == "Stone_Stunned")
            return baseName + suffix;

        return baseName;
    }

    //冲撞或眩晕结束后调用：已在原位就待机，否则走回去
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
        return StoneLineOfSight.IsVisibleInLine(grid, obstacleMask, origin, facing, detectRange, target, secondFloorTilemap);
    }

    public override EnemyState GetAlertState()//察觉后先进入激活，而不是直接冲撞
    {
        return activateState;
    }

    public void SuppressDetect(float duration)//撞到玩家后暂时不再察觉
    {
        nextDetectTime = Time.time + duration;
    }

    public override bool IsBusy()//激活 / 蓄力 / 冲撞 / 眩晕期间不响应噪音
    {
        EntityState current = stateMachine.currentState;
        return current == activateState || current == chargeState || current == stunnedState;
    }
}
