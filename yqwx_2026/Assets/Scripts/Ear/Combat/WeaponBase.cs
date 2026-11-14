using UnityEngine;

// 武器基类：把"所有武器都一样"的部分集中在这里，具体怎么打由子类实现。
// 负责三件事：
//   1) 冷却 cooldown —— 两次使用之间的最小间隔
//   2) 后摇 recovery —— 用完这段时间内玩家不能移动（由 HeldItemSlot 调 PlayerGridMove.LockMovement 锁住）
//   3) 使用次数     —— 次数用完武器自毁
//
// ★ 使用次数属于"物品"，不属于武器脚本。
//   数据存在 ItemData.maxUses / InventoryItem.remainingUses 里；
//   武器只在被装备时临时拿一个数字（ApplyItemUses），用完由 HeldItemSlot 读回去写进物品。
//   这样"放下再捡起 / 收进背包再取出"都不会把次数刷新回满。
public abstract class WeaponBase : MonoBehaviour, IUsable
{
    [Header("武器通用")]
    public float cooldown = 0.3f;   // 两次使用之间的最小间隔（秒）
    public float recovery = 0.15f;  // 后摇：用完这段时间内玩家不能移动（秒）

    // 兜底值：正常流程下会被物品数据覆盖（见 ApplyItemUses）。-1 = 无限次
    public int maxUses = -1;

    private float nextUseTime;       // 下一次可以使用的时间点
    private int remainingUses = -1;  // 还剩几次；-1 = 无限

    /// <summary>还剩几次（UI 显示用）</summary>
    public int RemainingUses => remainingUses;

    /// <summary>是不是无限次武器（例如刀）</summary>
    public bool IsInfinite => maxUses < 0;

    protected virtual void Awake()
    {
        remainingUses = maxUses;
    }

    /// <summary>
    /// 装备时由 HeldItemSlot 调用：以"物品数据"为准灌入次数。
    /// itemMaxUses 传 -1 表示无限；itemRemainingUses 传 -1 表示按满次数算。
    /// </summary>
    public void ApplyItemUses(int itemMaxUses, int itemRemainingUses)
    {
        maxUses = itemMaxUses;

        if (maxUses < 0)
        {
            remainingUses = -1;
            return;
        }

        int start = itemRemainingUses < 0 ? maxUses : itemRemainingUses;
        remainingUses = Mathf.Clamp(start, 0, maxUses);
    }

    // IUsable 的实现：返回本次的后摇时长（0 = 这次没打出去，不用后摇）
    public float Use(Vector2 origin, Vector2 facing)
    {
        if (Time.time < nextUseTime) return 0f;              // 还在冷却
        if (!IsInfinite && remainingUses <= 0) return 0f;    // 次数已经用完

        nextUseTime = Time.time + cooldown;
        if (!IsInfinite) remainingUses--;

        DoUse(origin, facing);   // 子类真正打出去

        if (!IsInfinite && remainingUses <= 0)
        {
            Debug.Log(name + " 次数耗尽，自我销毁");
            // 销毁后 HeldItemSlot 会在下一帧发现实例没了，把这件物品消耗掉（不回背包）
            Destroy(gameObject);
        }

        return recovery;         // 让 HeldItemSlot 去锁玩家移动
    }

    /// <summary>子类实现：怎么打出去（射线、区域判定、扔出弹丸……）</summary>
    protected abstract void DoUse(Vector2 origin, Vector2 facing);
}
