// 散落在地上的物品：玩家走近按 E（交互键）就捡起来。
//
// 规则：
//   ① 手上空着        → 装到手上
//   ② 手上有东西      → 放进背包
//   ③ 手上和背包都满  → 留在原地，什么都不变
using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    [Header("这是哪件物品")]
    public ItemData itemData;

    [Tooltip("这件物品还剩几次使用；-1 表示按满次数算（全新物品留 -1 即可）")]
    public int remainingUses = -1;

    public string GetPrompt()
    {
        return itemData != null ? "拾取 " + itemData.itemName : "拾取 物品";
    }

    public void Interact(GameObject interactor)
    {
        if (itemData == null)
        {
            Debug.LogWarning("Pickup 没有设置 itemData", this);
            return;
        }

        HeldItemSlot slot = interactor.GetComponent<HeldItemSlot>();
        if (slot == null) return;

        // 先给这件物品造一个"运行时实例"，把地上的剩余次数带过去
        InventoryItem item = new InventoryItem(itemData);
        if (remainingUses >= 0) item.SetRemainingUses(remainingUses);

        // ① 手上空 → 进手上
        if (slot.Equip(item))
        {
            Destroy(gameObject);
            return;
        }

        // ② 手上有 → 进背包（用 AddExistingItem，才会保留剩余次数）
        Inventory bag = FindFirstObjectByType<Inventory>();
        if (bag != null && bag.AddExistingItem(item))
        {
            Destroy(gameObject);
            return;
        }

        // ③ 都放不下 → 留在原地
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
