using UnityEngine;

// 测试用靶子：被刀或枪打中就死
public class TestEnemy : MonoBehaviour, IDamageable
{
    public void TakeHit(HitInfo info)
    {
        Debug.Log(name + " 被打中！来源：" + info.type + "，伤害：" + info.damage);
        Destroy(gameObject);
    }
}