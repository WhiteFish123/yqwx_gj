using UnityEngine;

public class Knife : MonoBehaviour, IUsable
{
    [Header("参数")]
    public float damage = 1f;//伤害
    public float reach = 2.0f;//能向前伸多远
    public float width = 2.0f;//攻击宽度
    public float cooldown = 2f;//冷却时间
    public float recovery =0.5f;//后摇时间
    private float nextuseTime;

    public float Use(Vector2 origin, Vector2 facing)//攻击
    {
        if (Time.time < nextuseTime) return 0f;
        nextuseTime = Time.time + cooldown;
        Vector2 center = origin + facing * reach * 0.5f;
        Vector2 size = facing.x!=0f
            ?new Vector2(reach, width)
            : new Vector2(width, reach);//向前伸的范围是一个矩形，矩形的长边是reach，短边是width
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size,0f);
        int count = 0;
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target == null) continue;

            HitInfo info = new HitInfo
            {
                damage = damage,
                type = HitType.knife
            };
            target.TakeHit(info);
            count++;
        }
        Debug.Log("挥刀，打中 " + count + " 个目标");
        return recovery;//返回后摇时间
    }

}