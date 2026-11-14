using UnityEngine;

// 飞行道具：子弹和铁球共用这一个脚本。
// 撞到东西 → 有 IDamageable 就造成伤害 → 生成命中特效 → 销毁自己。
public class Projectile : MonoBehaviour
{
    [Header("飞行")]
    public float lifeTime = 3f;//保险丝：飞太久自动消失

    [Header("伤害")]
    public HitType hitType = HitType.ironBall;//这个弹丸算哪种伤害（子弹选 gun）
    public float damage = 1f;                 //实际伤害由武器在 Launch 时覆盖

    [Header("命中特效")]
    [Tooltip("撞到东西时生成的特效 prefab，留空则没有特效")]
    public GameObject hitEffectPrefab;
    [Tooltip("特效多久后自动销毁（防止特效堆积）")]
    public float hitEffectLifeTime = 0.3f;

    private Rigidbody2D rb;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    public void Launch(Vector2 direction, float speed, float damageValue)
    {
        damage = damageValue;

        if (rb != null) rb.linearVelocity = direction.normalized * speed;

        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) return;//别打自己

        IDamageable target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            HitInfo info = new HitInfo();
            info.damage = damage;
            info.type = hitType;
            target.TakeHit(info);
        }

        SpawnHitEffect();
        Destroy(gameObject);
    }

    //生成命中特效（无论有没有特效配置都不会报错）
    void SpawnHitEffect()
    {
        if (hitEffectPrefab == null) return;

        GameObject fx = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        Destroy(fx, hitEffectLifeTime);//★ 一定要定时销毁，否则特效会越堆越多
    }
}