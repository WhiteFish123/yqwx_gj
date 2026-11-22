using System.Collections.Generic;
using UnityEngine;

//爱丽丝：索敌范围与攻击方式与史莱姆一致，动画按朝向切换（前 / 后 / 左右）
public class Enemy_Alice : Enemy
{
    //动画名：朝向后缀（左右不区分，靠 SpriteRenderer.flipX 翻转）
    private const string IdleName = "Alice_Idle";
    private const string ChargeName = "Alice_Charge";
    private const string CrashName = "Alice_Crash";

    [Header("Boss")]
    [Tooltip("没开战前站着的朝向（默认朝下）")]
    public Vector2Int initialFacing = Vector2Int.down;
    [Tooltip("勾上＝一开始就能打；关掉＝等剧情播完调用 StartFight() 才参战（BOSS 用）")]
    public bool combatEnabled;


    protected override void Awake()//创建状态实例
    {
        base.Awake();

        //这些状态不用 Animator 的 bool 参数，直接按名字播放（名字会按朝向解析）
        idleState = new Enemy_IdleState(this, stateMachine, null, IdleName);
        moveState = new Enemy_MoveState(this, stateMachine, null, IdleName);
        chaseState = new Enemy_ChaseState(this, stateMachine, null, IdleName);
        attackState = new Enemy_AliceAttackState(this, stateMachine, ChargeName, IdleName);
        returnState = new Enemy_ReturnState(this, stateMachine, null, IdleName);
        investigateState = new Enemy_InvestigateState(this, stateMachine, null, IdleName);
        deadState = new Enemy_DeadState(this, stateMachine, CrashName, 2f);
    }

    protected override void Start()//构建巡逻路线并进入初始状态
    {
        base.Start();

        SetFacing(initialFacing);//先按设定朝站好（BOSS 默认朝下）

        if (combatEnabled)
            BuildPatrolPath();//没开战就不建路线，站着不动等剧情

        stateMachine.Initialize(idleState);
    }

    //转向玩家：优先横向（左右比上下明显时朝左右），并立刻换成对应朝向的待机图
    public void FacePlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        Vector2 delta = (Vector2)(player.transform.position - transform.position);

        if (delta.sqrMagnitude < 0.0001f)
            return;

        Vector2Int direction;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            direction = delta.x >= 0f ? Vector2Int.right : Vector2Int.left;
        else
            direction = delta.y >= 0f ? Vector2Int.up : Vector2Int.down;

        SetFacing(direction);
        RefreshFacingClip();
    }

    //剧情播完调用：开始参战
    public void StartFight()
    {
        if (combatEnabled)
            return;

        combatEnabled = true;

        BuildPatrolPath();
        FacePlayer();
    }

    //没开战前对玩家完全没反应
    public override bool CanDetectPlayer()
    {
        if (!combatEnabled)
            return false;

        return base.CanDetectPlayer();
    }

    protected override void Update()
    {
        base.Update();

        RefreshFacingClip();
    }

    //状态只在进入时播一次动画，走动转身后要把当前动作换成对应朝向的 clip
    private void RefreshFacingClip()
    {
        if (anim == null || stateMachine == null)
            return;

        EntityState current = stateMachine.currentState;

        //只有待机/移动这类「站姿」状态需要跟随朝向，冲刺与死亡动画不换
        bool locomotion = current != null
            && (current == idleState || current == moveState || current == chaseState
                || current == returnState || current == investigateState);

        if (!locomotion)
            return;

        string wanted = ResolveAnimName(IdleName);

        if (anim.GetCurrentAnimatorStateInfo(0).IsName(wanted))
            return;

        anim.Play(wanted);
    }

    //按朝向把基名换成实际 clip：上下用专用图，左右共用侧面图（flipX 翻转）
    public override string ResolveAnimName(string baseName)
    {
        if (string.IsNullOrEmpty(baseName))
            return baseName;

        if (baseName != IdleName && baseName != ChargeName)
            return baseName;//死亡等动画不做朝向区分

        if (facing == Vector2Int.up)
            return baseName + "_Back";

        if (facing == Vector2Int.down)
            return baseName + "_Front";

        return baseName + "_Side";
    }

    //索敌范围：比史莱姆更长的扇形（5 排宽，最远 7 格，中间 3 排含身后 1 格）
    protected override List<Vector2Int> GetSightCells(Vector3Int origin, Vector2Int facing)
    {
        if (vision == null)
            return new List<Vector2Int>();

        return vision.GetAliceSightCells(origin, facing);
    }
}
