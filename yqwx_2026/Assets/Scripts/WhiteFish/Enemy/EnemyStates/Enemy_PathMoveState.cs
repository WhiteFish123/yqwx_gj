using System.Collections.Generic;
using UnityEngine;

//逐格移动基类：RPG Maker 风格，格与格之间平滑插值，不使用物理速度
public abstract class Enemy_PathMoveState : EnemyState
{
    private Vector3 stepFrom;//本步起点（格中心）
    private Vector3 stepTo;//本步终点（格中心）
    private float progress;//本步进度 0~1
    private bool isStepping;//是否正在跨格

    public Enemy_PathMoveState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }

    protected abstract float StepDuration { get; }//每走一格用时

    protected abstract Vector3Int? GetTargetCell();//目标格，null 表示没有目标

    protected virtual bool ShouldAbort()//是否中断，子类可覆盖
    {
        return false;
    }

    protected virtual void OnArrived()//已在目标格
    {
    }

    protected virtual void OnNoTarget()//没有目标或走不过去
    {
    }

    public override void Enter()
    {
        base.Enter();

        isStepping = false;
        progress = 0f;
    }

    public override void Exit()
    {
        isStepping = false;
        progress = 0f;

        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (ShouldAbort())
        {
            isStepping = false;
            return;
        }

        if (isStepping)//跨格中：只做插值，不重新决策
        {
            progress += Time.deltaTime / Mathf.Max(0.01f, StepDuration);
            enemy.transform.position = Vector3.Lerp(stepFrom, stepTo, Mathf.Clamp01(progress));

            if (progress >= 1f)
            {
                enemy.transform.position = stepTo;
                isStepping = false;
                progress = 0f;
            }

            return;
        }

        Vector3Int? target = GetTargetCell();
        if (target == null)
        {
            OnNoTarget();
            return;
        }

        Vector3Int current = enemy.WorldToCell(enemy.transform.position);

        if (current == target.Value)//已到目标格，停住
        {
            OnArrived();
            return;
        }

        List<Vector3Int> path = enemy.pathfinder.FindPath(current, target.Value);
        if (path == null || path.Count < 2)//不可达
        {
            OnNoTarget();
            return;
        }

        Vector3Int next = path[1];//永远走"下一格"

        if (enemy.IsCellOccupied(next))//被玩家或别的敌人占着，等它让开
            return;

        BeginStep(current, next);
    }

    private void BeginStep(Vector3Int from, Vector3Int to)//开始跨一格
    {
        stepFrom = enemy.CellToCenter(from);
        stepTo = enemy.CellToCenter(to);
        progress = 0f;
        isStepping = true;

        enemy.SetFacing(ToFacing(to - from));
    }

    protected static Vector2Int ToFacing(Vector3Int delta)//格坐标增量转四向朝向
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x >= 0 ? Vector2Int.right : Vector2Int.left;

        return delta.y >= 0 ? Vector2Int.up : Vector2Int.down;
    }
}
