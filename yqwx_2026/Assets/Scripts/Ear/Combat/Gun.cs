using UnityEngine;

// 枪：发射实体子弹（不再用射线）。
// 射程 = 子弹速度 × 子弹存活时间，都在 Bullet.prefab 上配。
public class Gun : WeaponBase
{
    [Header("枪")]
    public GameObject bulletPrefab;   //拖 Bullet.prefab
    public float damage = 5f;//实际伤害由子弹在 Launch 时覆盖
    public float bulletSpeed = 16f;//子弹飞行速度（单位：米/秒）
    public float spawnOffset = 0.6f;  //从身前多远生成，避免一出生就打到自己

    protected overr
        ide void DoUse(Vector2 origin, Vector2 facing)
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("枪没有设置 Bullet Prefab，开不出子弹", this);
            return;
        }

        GameObject go = Instantiate(bulletPrefab, origin + facing * spawnOffset, Quaternion.identity);

        Projectile bullet = go.GetComponent<Projectile>();
        if (bullet != null) bullet.Launch(facing, bulletSpeed, damage);
    }
}