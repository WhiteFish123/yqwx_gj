//这个脚本挂在地上的物品上，玩家走过去按交互键就能捡起来
using UnityEngine;

// 散落在地上的任何物品：武器、以后的血瓶、钥匙……共用这一个
// 规则：手上空 → 放到手上；手上有 → 手上不变，这件进背包（现在没背包，就先留在地上）
public class Pickup : MonoBehaviour, IInteractable
{
    [Header("这是哪个物品")]
    public GameObject itemPrefab;
    public string displayName = "物品";

    public string GetPrompt()
    {
        return "拾取 " + displayName;
    }

    public void Interact(GameObject interactor)//机制：手上空 → 放到手上；手上有 → 手上不变，这件进背包（现在没背包，就先留在地上）
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("Pickup 的 itemPrefab 没设置", this);
            return;
        }

        HeldItemSlot slot = interactor.GetComponent<HeldItemSlot>();
        if (slot == null) return;

        if (slot.Equip(itemPrefab))
        {
            Destroy(gameObject);
            return;
        }
        // 背包还没做的逻辑：这件留在原地，玩家手上的东西完全不变，等有背包后改这里
        Debug.Log("手上已有 " + slot.HeldItemName + "，背包还没做好，先留在地上：" + displayName);
    }
}