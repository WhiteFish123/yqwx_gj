using UnityEngine;

// 刀：近战武器，在玩家正前方扫一块矩形区域，区域内所有可受击对象都吃伤害。
// 使用次数、冷却、后摇都由 WeaponBase 统一管理（刀在物品数据 ItemData 里配成 -1 = 无限次）。
public class Knife : WeaponBase
{
    [Header("刀")]
    public float damage = 1f;    // 命中伤害
    public float reach = 2f;     // 往前够多远（矩形的长边）
    public float width = 2f;     // 判定框有多宽（矩形的短边）

    protected override void DoUse(Vector2 origin, Vector2 facing)
    {
        // 判定框的中心：从使用者中心往朝向前方推半个 reach
        Vector2 center = origin + facing * (reach * 0.5f);

        // 朝左右时是"横条"，朝上下时是"竖条" —— 长边永远顺着朝向
        Vector2 size = facing.x != 0f
            ? new Vector2(reach, width)
            : new Vector2(width, reach);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);

        int count = 0;
        foreach (Collider2D hit in hits)
        {
            // 只有实现 IDamageable 的物体才算命中；墙、地板、地上的物品都会被跳过
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target == null) continue;

            HitInfo info = new HitInfo();
            info.damage = damage;
            info.type = HitType.knife;
            target.TakeHit(info);
            count++;
        }

        Debug.Log("挥刀：命中 " + count + " 个目标");
    }
}
