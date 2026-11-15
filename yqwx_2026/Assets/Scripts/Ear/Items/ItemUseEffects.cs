using UnityEngine;

// 道具在背包里点"使用"时触发的效果。
//
// 为什么单独抽出来：
//   Inventory.UseItem 只需要"用一下试试，成功了才扣数量"，
//   至于具体回血/解毒/加buff，都收在这里，以后加新道具只改这一个 switch。
public static class ItemUseEffects
{
    //返回 true = 这次使用生效了（可以扣数量）；
    //返回 false = 没生效（比如满血喝血药），不消耗。
    public static bool Apply(ItemData data)
    {
        if (data == null)
            return false;

        switch (data.useEffect)
        {
            case ItemEffect.Heal:
                PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
                if (health == null)
                    return false;

                //满血：不消耗道具，弹一句提示（和"手上按 J 喝"保持一致）
                if (health.currentHealth >= health.maxHealth)
                {
                    ScreenMessage.Show("我感觉良好，不需要使用这个");
                    return false;
                }

                return health.Heal(Mathf.Max(1, data.effectAmount));

            default:
                //没有效果的道具：允许正常消耗（保持原来的行为）
                return true;
        }
    }
}
