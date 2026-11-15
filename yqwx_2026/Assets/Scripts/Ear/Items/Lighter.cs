using UnityEngine;

// 打火机：拿在手上按 J，点燃正前方一格的箱子。
//
// 为什么继承 WeaponBase：
//   冷却、后摇（用完锁移动）、按 J 的整套流程都能直接复用，
//   区别只在 DoUse —— 它不造成伤害，而是去点火。
public class Lighter : WeaponBase
{
    [Header("打火机")]
    [Tooltip("往前第几格点火：1 = 正前方一格")]
    public int reach = 1;

    private PlayerGridMove motor;

    protected override void Awake()
    {
        base.Awake();
        motor = FindFirstObjectByType<PlayerGridMove>();
    }

    protected override void DoUse(Vector2 origin, Vector2 facing)
    {
        if (motor == null)
        {
            Debug.LogWarning("打火机：场景里找不到 PlayerGridMove，点不了火", this);
            return;
        }

        //把朝向取整成四方向（facing 是单位向量，可能带浮点误差）
        Vector2Int dir = new Vector2Int(Mathf.RoundToInt(facing.x), Mathf.RoundToInt(facing.y));
        if (dir == Vector2Int.zero)
            dir = Vector2Int.down;

        //用玩家自己的格子坐标：一个场景里有多个 Grid，自己算 WorldToCell 会选错房间
        Vector3Int originCell = motor.Cell;
        Vector2Int target = new Vector2Int(originCell.x + dir.x * reach, originCell.y + dir.y * reach);

        Chest chest = Chest.At(target);

        if (chest == null)
            return;//面前没有箱子：什么也不做（不消耗任何东西）

        chest.Ignite();
        Debug.Log("打火机点燃了面前的箱子");
    }
}
