// 散落在地上的物品：玩家走近按 E（交互键）就捡起来。
//
// 规则：
//   ① 手上空着        → 装到手上
//   ② 手上有东西      → 放进背包
//   ③ 手上和背包都满  → 提示"没有多余的空间"，按 E 不做任何事
//
// 提示文字会带上剩余使用次数（例：拾取手枪（4/6）），无限次物品（刀）不显示括号。
using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    [Header("这是哪件物品")]
    public ItemData itemData;

    [Tooltip("这件物品还剩几次使用；-1 表示按满次数算（全新物品留 -1 即可）")]
    public int remainingUses = -1;

    private Inventory bag;//缓存背包，避免每帧 FindFirstObjectByType
    private HeldItemSlot slot;//缓存手持栏

    private void Start()
    {
        EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (bag == null) bag = FindFirstObjectByType<Inventory>();
        if (slot == null) slot = FindFirstObjectByType<HeldItemSlot>();
    }

    //还有没有地方放：手上空着，或者背包有空格
    private bool HasRoom()
    {
        EnsureRefs();

        if (slot != null && !slot.HasItem)
            return true;

        return bag != null && bag.HasEmptySlot();
    }

    public string GetPrompt()
    {
        if (itemData == null)
            return "拾取物品";

        //没地方放：直接把原因写在提示里，按 E 也不会有效果
        if (!HasRoom())
            return "你没有多余的空间容纳" + itemData.itemName + "了";

        //无限次物品（刀）不显示次数
        if (itemData.maxUses < 0)
            return "拾取" + itemData.itemName;

        int uses = remainingUses >= 0 ? remainingUses : itemData.maxUses;
        return "拾取" + itemData.itemName + "（" + uses + "/" + itemData.maxUses + "）";
    }

    public void Interact(GameObject interactor)
    {
        if (itemData == null)
        {
            Debug.LogWarning("Pickup 没有设置 itemData", this);
            return;
        }

        HeldItemSlot hand = interactor.GetComponent<HeldItemSlot>();
        if (hand == null) return;

        //放不下就什么都不做（提示文字已经说明原因）
        if (!HasRoom()) return;

        //先给这件物品造一个"运行时实例"，把地上的剩余次数带过去
        InventoryItem item = new InventoryItem(itemData);
        if (remainingUses >= 0) item.SetRemainingUses(remainingUses);

        //① 手上空 → 进手上
        if (hand.Equip(item))
        {
            Destroy(gameObject);
            return;
        }

        //② 手上有 → 进背包（用 AddExistingItem 才会保留剩余次数）
        if (bag != null && bag.AddExistingItem(item))
        {
            Destroy(gameObject);
            return;
        }

        //③ 理论上走不到这里（HasRoom 已经拦过），保险起见留在原地
        Debug.Log("手上和背包都放不下：" + itemData.itemName);
    }

    // ===== 给"背包丢弃"用的静态方法 =====
    /// <summary>把一件物品放回地面，并让它记住自己的剩余次数</summary>
    public static Pickup Spawn(ItemData data, int uses, Vector3 position)
    {
        if (data == null || data.worldPrefab == null)
        {
            Debug.LogWarning("Pickup.Spawn 失败：itemData 或 worldPrefab 为空");
            return null;
        }

        GameObject go = Instantiate(data.worldPrefab, position, Quaternion.identity);

        Pickup pickup = go.GetComponent<Pickup>();
        if (pickup == null)
        {
            Debug.LogWarning("地上的 prefab 上没有挂 Pickup 组件：" + data.worldPrefab.name);
            return null;
        }

        pickup.itemData = data;
        pickup.remainingUses = uses;
        return pickup;
    }
}
