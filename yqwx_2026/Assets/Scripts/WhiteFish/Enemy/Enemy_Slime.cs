using System.Collections.Generic;
using UnityEngine;

//史莱姆：创建自身所需状态，并在开始时进入待机
public class Enemy_Slime : Enemy
{
    protected override void Awake()//创建状态实例
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, "Idle");
        moveState = new Enemy_MoveState(this, stateMachine, "Move");
        chaseState = new Enemy_ChaseState(this, stateMachine, "Chase");
        attackState = new Enemy_AttackState(this, stateMachine, "Slime_Attack", "Slime_Move");
        returnState = new Enemy_ReturnState(this, stateMachine, "Move");
        investigateState = new Enemy_InvestigateState(this, stateMachine, "Move");//调查声音，复用走路动画
        deadState = new Enemy_DeadState(this, stateMachine, "Slime_Dead", 2f);
    }

    protected override void Start()//构建巡逻路线并进入初始状态
    {
        base.Start();

        BuildPatrolPath();
        stateMachine.Initialize(idleState);
    }

    //史莱姆索敌范围：以「朝右」为例，中间 3 排覆盖「身后 1 格 ~ 前方 3 格」，
    //上下 2 排只覆盖前方 1~3 格（四个角切掉），整体随朝向旋转
    protected override List<Vector2Int> GetSightCells(Vector3Int origin, Vector2Int facing)
    {
        if (vision == null)
            return new List<Vector2Int>();

        return vision.GetSlimeSightCells(origin, facing);
    }
}
