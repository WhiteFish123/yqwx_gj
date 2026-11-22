using UnityEngine;

//石像鬼原路退回：冲撞把它推进了「走不回去」的区域（区域表没覆盖、楼梯口之类）时，
//顺着本次冲撞的轨迹一格一格退回去，退回能正常寻路的地方再交给返回状态走回原位。
public class Enemy_StoneRetreatState : EnemyState
{
    private readonly Enemy_Stone stone;//轨迹与速度在子类上

    private Vector3 stepFrom;//本步起点
    private Vector3 stepTo;//本步终点
    private float progress;//本步进度 0~1
    private bool isStepping;//是否正在跨格

    public Enemy_StoneRetreatState(Enemy_Stone stone, StateMachine stateMachine, string animPlayName = null)
        : base(stone, stateMachine, null, animPlayName)//不参 bool 参数体系
    {
        this.stone = stone;
    }

    public override void Enter()
    {
        base.Enter();

        isStepping = false;
        progress = 0f;

        if (enemy.rb != null)
            enemy.rb.linearVelocity = Vector2.zero;

        //轨迹最后一项就是它现在站的格子，先弹掉，免得原地站着再弹一次
        int last = stone.dashTrail.Count - 1;
        if (last >= 0 && stone.dashTrail[last] == enemy.WorldToCell(enemy.transform.position))
            stone.dashTrail.RemoveAt(last);
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

        if (isStepping)//跨格中：只做插值
        {
            progress += Time.deltaTime / Mathf.Max(0.01f, stone.retreatDuration);
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

        if (current == stone.homeCell)//已经退回原位
        {
            stone.GoHome();
            return;
        }

        if (stone.dashTrail.Count == 0)//轨迹用完还是回不去：直接归位，避免卡死在半路
        {
            enemy.transform.position = stone.CellToCenter(stone.homeCell);
            stone.GoHome();
            return;
        }

        int lastIndex = stone.dashTrail.Count - 1;
        Vector3Int previous = stone.dashTrail[lastIndex];
        stone.dashTrail.RemoveAt(lastIndex);

        if (previous == current)
            return;

        stepFrom = enemy.CellToCenter(current);
        stepTo = enemy.CellToCenter(previous);
        enemy.SetFacing(new Vector2Int(previous.x - current.x, previous.y - current.y));
        progress = 0f;
        isStepping = true;
    }
}
