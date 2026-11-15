using UnityEngine;

//石像鬼返回原位：冲刺结束或晕眩结束后逐格走回出生格子
public class Enemy_StoneReturnState : Enemy_PathMoveState
{
    private readonly Enemy_Stone stone;//原位与速度在子类上

    public Enemy_StoneReturnState(Enemy_Stone stone, StateMachine stateMachine, string animBoolName, string animPlayName = null)
        : base(stone, stateMachine, animBoolName, animPlayName)
    {
        this.stone = stone;
    }

    protected override float StepDuration => stone.moveDuration;//返回途中用巡逻速度

    protected override Vector3Int? GetTargetCell()
    {
        return stone.homeCell;
    }

    protected override void OnArrived()//回到原位，恢复待机
    {
        stateMachine.ChangeState(stone.idleState);
    }

    protected override void OnNoTarget()//走不回去（被堵住），就地待机，避免卡死
    {
        stateMachine.ChangeState(stone.idleState);
    }
}
