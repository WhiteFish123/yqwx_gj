using UnityEngine;

// 刀：近战武器，在玩家正前方扫一块矩形区域，区域内所有可受击对象都吃伤害。
// 使用次数、冷却、后摇都由 WeaponBase 统一管理（刀在物品数据 ItemData 里配成 -1 = 无限次）。
public class Knife : WeaponBase
{
    [Header("刀")]
    public float damage = 1f;    // 命中伤害
    public float reach = 2f;     // 往前够多远（矩形的长边）
    public float width = 2f;     // 判定框有多宽（矩形的短边）

    [Header("刀光特效")]
    [Tooltip("刀光 prefab，留空则不放特效")]
    public GameObject slashPrefab;

    [Tooltip("刀光离角色多远（默认和判定框中心重合 = reach 的一半）")]
    public float slashDistance = 0.75f;

    [Tooltip("刀光生成后多久销毁（要略大于动画时长）")]
    public float slashLifeTime = 0.35f;

    [Tooltip("贴图默认朝向的补偿角度。Play 后如果刀光方向不对，按 90 的倍数调这个数")]
    public float slashRotationOffset = -45f;

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
            //★ 别砍到自己：手里的刀是玩家的子物体，所以两者的 root 是同一个。
            //  （玩家现在实现了 IDamageable，不跳过的话一刀会砍自己一滴血）
            if (hit.transform.root == transform.root) continue;

            // 只有实现 IDamageable 的物体才算命中；墙、地板、地上的物品都会被跳过
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target == null) continue;

            HitInfo info = new HitInfo();
            info.damage = damage;
            info.type = HitType.knife;
            target.TakeHit(info);
            count++;
        }

        SpawnSlash(origin, facing);

        Debug.Log("挥刀：命中 " + count + " 个目标");
    }

    //在角色前方放一段刀光。
    //位置用和判定框同一个中心点，角度按朝向旋转（右=0° 上=90° 左=180° 下=-90°）。
    private void SpawnSlash(Vector2 origin, Vector2 facing)
    {
        if (slashPrefab == null)
            return;

        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + slashRotationOffset;
        Vector3 position = origin + facing * slashDistance;

        GameObject fx = Instantiate(slashPrefab, position, Quaternion.Euler(0f, 0f, angle));

        Destroy(fx, slashLifeTime);//播完就删，免得越堆越多
    }
}
