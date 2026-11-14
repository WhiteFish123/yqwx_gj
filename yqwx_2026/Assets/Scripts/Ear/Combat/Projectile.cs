using UnityEngine;

// 飞行道具：铁球扔出去之后就是它自己飞。
// 撞到东西 → 若对方实现 IDamageable 就造成伤害 → 无论撞到什么都销毁自己。
//
// ★ prefab 上必须有：
//   - Rigidbody2D：Body Type = Dynamic、Gravity Scale = 0、Collision Detection = Continuous
//     （Kinematic 与 Static 碰撞体之间不会产生 Trigger 事件，球会穿墙）
//   - Collider2D：勾上 Is Trigger，否则会把玩家顶开
public class Projectile : MonoBehaviour
{
    [Header("飞行")]
    public float lifeTime = 3f;   // 保险丝：飞太久自动消失，防止场景里堆积飞不出去的球

    private Rigidbody2D rb;
    private float damage;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>由武器（IronBall）在生成后立刻调用，交给它方向和速度</summary>
    public void Launch(Vector2 direction, float speed, float damageValue)
    {
        damage = damageValue;

        if (rb != null) rb.linearVelocity = direction.normalized * speed;

        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 别打自己：玩家身上有 "Player" 标签（队友的敌人 AI 也用这个标签）
        if (other.CompareTag("Player")) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            HitInfo info = new HitInfo();
            info.damage = damage;
            info.type = HitType.ironBall;
            target.TakeHit(info);
        }

        Destroy(gameObject);   // 撞到敌人、墙、箱子都消失
    }
}
