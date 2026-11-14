using UnityEngine;

// 枪：远程武器，射线即时命中 —— 打出去的那一瞬间就判定，没有飞行时间。
// 使用次数、冷却、后摇都由 WeaponBase 统一管理，这里只负责"怎么打出去"。
public class Gun : WeaponBase
{
    [Header("枪")]
    public float damage = 5f;           // 命中伤害
    public float range = 10f;           // 射程
    public float startOffset = 0.6f;    // 射线从身前多远开始，避免打到自己的身体
    public LayerMask hitLayers = ~0;    // 只检测这些层；想防止被地板挡住，就取消勾选地板所在的层

    protected override void DoUse(Vector2 origin, Vector2 facing)
    {
        // 从"使用者中心 + 朝向偏移"射出，方向就是玩家面向
        Vector2 start = origin + facing * startOffset;
        RaycastHit2D hit = Physics2D.Raycast(start, facing, range, hitLayers);

        if (hit.collider == null)
        {
            Debug.Log("开枪：没打中");
            return;
        }

        // 只有实现了 IDamageable 的东西才会受伤（墙、地板不实现，所以只是"挡住"）
        IDamageable target = hit.collider.GetComponent<IDamageable>();
        if (target != null)
        {
            HitInfo info = new HitInfo();
            info.damage = damage;
            info.type = HitType.gun;
            target.TakeHit(info);
        }

        Debug.Log("开枪：打中 " + hit.collider.name);
    }
}
