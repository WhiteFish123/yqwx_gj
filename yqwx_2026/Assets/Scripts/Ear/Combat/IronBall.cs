using UnityEngine;

// 铁球：远程投掷武器。扔出一个实体球，球飞到哪里撞到什么才结算。
//
// 和枪的区别：
//   - 枪是"射线即时命中"，按下去立刻有结果；
//   - 铁球有真的飞行过程，敌人可以躲，玩家能看清轨迹。
// 三把武器里它的冷却和后摇最短（见 ItemData 那边的配置），但只有 2 次，属于应急火力。
//
// 次数、冷却、后摇都由 WeaponBase 管，这里只负责"把球扔出去"。
public class IronBall : WeaponBase
{
    [Header("铁球")]
    public GameObject projectilePrefab;  // 扔出去的那个球（上面要有 Projectile 脚本）
    public float damage = 8f;            // 命中伤害（比刀高比枪高，属于"重击"）
    public float speed = 9f;             // 飞行速度，越大越快
    public float spawnOffset = 0.7f;     // 从身前多远生成，避免一出生就撞到自己

    protected override void DoUse(Vector2 origin, Vector2 facing)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("铁球没有设置 projectilePrefab，扔不出去", this);
            return;
        }

        // 出生点：使用者中心往朝向前方推一点
        Vector3 spawnPos = origin + facing * spawnOffset;
        GameObject ball = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        // 把方向和速度交给球自己飞
        Projectile projectile = ball.GetComponent<Projectile>();
        if (projectile != null) projectile.Launch(facing, speed, damage);
    }
}
