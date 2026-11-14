using UnityEngine;

public class Gun : MonoBehaviour, IUsable
{
    [Header("参数")]
    public float damage = 5f;
    public float range = 10f;// 射程
    public float fireRate = 4f;// 每秒几发
    public float recovery = 0.3f;// 后摇
    public float startOffset = 0.6f;// 从身前多远开始射

    private float nextUseTime;

    public float Use(Vector2 origin, Vector2 facing)
    {
        if (Time.time < nextUseTime) return 0f;                 // 冷却中
        nextUseTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

        Vector2 start = origin + facing * startOffset;
        RaycastHit2D hit = Physics2D.Raycast(start, facing, range);

        if (hit.collider != null)
        {
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                HitInfo info = new HitInfo();
                info.damage = damage;
                info.type = HitType.gun;
                target.TakeHit(info);
            }
            Debug.Log("开枪，打中 " + hit.collider.name);
        }
        else
        {
            Debug.Log("开枪，没打中");
        }

        return recovery;
    }
}