using UnityEngine;

//攻击状态：起手播到第二帧后原地停顿，再高速冲刺，最后等攻击动画播完回到追逐
public class Enemy_AttackState : EnemyState
{
    private enum Phase
    {
        Windup,//起手，播到第二帧
        Hold,//原地停顿
        Dash,//高速冲刺
        Recover//等剩余动画播完
    }

    private readonly string attackAnimName;//冲刺动画
    private readonly string recoverAnimName;//结束后恢复的动画

    private Phase phase;//当前阶段
    private float phaseTimer;//当前阶段剩余时间

    private Vector2Int direction;//冲刺方向
    private int cellsMoved;//已冲格数
    private Vector3 stepFrom;//本步起点
    private Vector3 stepTo;//本步终点
    private float progress;//本步进度
    private bool isStepping;//是否正在跨格

    private float elapsed;//攻击已进行时间
    private bool originalFlipX;//进入前的精灵朝向

    public Enemy_AttackState(Enemy enemy, StateMachine stateMachine, string attackAnimName, string recoverAnimName)
        : base(enemy, stateMachine, null)//攻击不参与 bool 参数体系
    {
        this.attackAnimName = attackAnimName;
        this.recoverAnimName = recoverAnimName;
    }

    public override void Enter()
    {
        base.Enter();

        direction = enemy.facing;
        cellsMoved = 0;
        isStepping = false;
        progress = 0f;
        elapsed = 0f;

        phase = Phase.Windup;
        phaseTimer = enemy.attackWindupDelay;

        if (enemy.spriteRenderer != null)
        {
            originalFlipX = enemy.spriteRenderer.flipX;

            if (enemy.flipAttackSprite)//攻击美术方向与移动相反时翻转
                enemy.spriteRenderer.flipX = !originalFlipX;
        }

        if (anim != null)
        {
            anim.speed = 1f;

            if (!string.IsNullOrEmpty(attackAnimName))
                anim.Play(attackAnimName);
        }
    }

    public override void Exit()
    {
        isStepping = false;
        progress = 0f;

        if (anim != null)
            anim.speed = 1f;//无论停在哪个阶段都要恢复动画速度

        if (enemy.spriteRenderer != null)//还原翻转，交给后续状态重新设置
            enemy.spriteRenderer.flipX = originalFlipX;

        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        elapsed += Time.deltaTime;

        switch (phase)
        {
            case Phase.Windup:
                UpdateWindup();
                return;

            case Phase.Hold:
                UpdateHold();
                return;

            case Phase.Recover:
                phaseTimer -= Time.deltaTime;

                if (phaseTimer <= 0f)
                    Finish();

                return;

            default:
                UpdateDash();
                return;
        }
    }

    private void UpdateWindup()//播到第二帧后暂停动画
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f)
            return;

        if (anim != null)
            anim.speed = 0f;//冻在第二帧

        phase = Phase.Hold;
        phaseTimer = enemy.attackHoldTime;
    }

    private void UpdateHold()//原地站住，时间到后恢复动画并开冲
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f)
            return;

        if (anim != null)
            anim.speed = 1f;//继续播放剩下的动画

        phase = Phase.Dash;
    }

    private void UpdateDash()//逐格冲刺
    {
        if (isStepping)
        {
            progress += Time.deltaTime / Mathf.Max(0.01f, enemy.attackDashDuration);
            enemy.transform.position = Vector3.Lerp(stepFrom, stepTo, Mathf.Clamp01(progress));

            if (progress >= 1f)
            {
                enemy.transform.position = stepTo;
                isStepping = false;
                progress = 0f;
                cellsMoved++;

                if (cellsMoved >= enemy.attackDashCells)
                    RequestFinish();
            }

            return;
        }

        if (cellsMoved >= enemy.attackDashCells)//冲满了
        {
            RequestFinish();
            return;
        }

        Vector3Int current = enemy.WorldToCell(enemy.transform.position);
        Vector3Int next = current + new Vector3Int(direction.x, direction.y, 0);

        if (enemy.IsCellOccupied(next))//前方被障碍、玩家或别的敌人挡住
        {
            RequestFinish();
            return;
        }

        stepFrom = enemy.CellToCenter(current);
        stepTo = enemy.CellToCenter(next);
        progress = 0f;
        isStepping = true;
    }

    private void RequestFinish()//冲刺结束：等动画补完再收尾
    {
        float expectedTotal = enemy.attackAnimLength + enemy.attackHoldTime;//停顿不计入动画时长
        float remaining = expectedTotal - elapsed;

        if (remaining > 0.01f)
        {
            phase = Phase.Recover;
            phaseTimer = remaining;
            return;
        }

        Finish();
    }

    private void Finish()//恢复移动动画并继续追逐
    {
        enemy.BeginAttackCooldown();//进入冷却，期间不会再次冲刺

        if (anim != null)
            anim.speed = 1f;

        if (enemy.spriteRenderer != null)
            enemy.spriteRenderer.flipX = originalFlipX;

        if (anim != null && !string.IsNullOrEmpty(recoverAnimName))
            anim.Play(recoverAnimName);

        stateMachine.ChangeState(enemy.chaseState);
    }
}
