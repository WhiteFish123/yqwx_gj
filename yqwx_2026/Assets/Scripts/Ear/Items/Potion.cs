using UnityEngine;

// 血药：拿在手上按 J 喝掉，回 healAmount 点血。
//
// 使用次数由物品数据决定（血药的 maxUses = 1）：
// 喝完后 WeaponBase 会让武器实例自毁，HeldItemSlot 随后把这件物品消耗掉。
//
// 想从背包"使用"也生效，需要两处配合：
//   1) ItemData.useEffect = Heal（ItemUseEffects 会在 Inventory.UseItem 里回血）
//   2) UI 的按钮对消耗品优先显示"使用"
public class Potion : WeaponBase
{
    [Header("血药")]
    [Tooltip("回复几点血")]
    public int healAmount = 1;

    //满血时不允许喝：不消耗、不进冷却，只在屏幕上弹一句提示
    protected override bool CanUse(Vector2 origin, Vector2 facing)
    {
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health == null)
            return false;

        if (health.currentHealth >= health.maxHealth)
        {
            ScreenMessage.Show("我感觉良好，不需要使用这个");
            return false;
        }

        return true;
    }

    protected override void DoUse(Vector2 origin, Vector2 facing)
    {
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();

        if (health == null)
        {
            Debug.LogWarning("血药：场景里找不到 PlayerHealth", this);
            return;
        }

        health.Heal(healAmount);
    }
}
