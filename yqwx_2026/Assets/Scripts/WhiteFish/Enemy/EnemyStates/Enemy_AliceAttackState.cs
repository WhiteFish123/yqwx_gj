using UnityEngine;

//爱丽丝的冲刺：和史莱姆一样蓄力-冲刺-收招，但冲刺不限格数，一直冲到撞到东西为止
public class Enemy_AliceAttackState : Enemy_AttackState
{
    //基类用 enemy.attackDashCells 限制冲刺格数，这里放开
    protected override int DashCellLimit
    {
        get { return int.MaxValue; }
    }

    public Enemy_AliceAttackState(Enemy enemy, StateMachine stateMachine, string attackAnimName, string recoverAnimName)
        : base(enemy, stateMachine, attackAnimName, recoverAnimName)
    {
    }
}
