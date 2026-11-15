using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 右下角的"手上道具"提示框。
//
// 显示内容：
//   - 地上那件道具的图案（直接取 worldPrefab 上的 SpriteRenderer 的 sprite 和颜色）
//   - 名字；有使用次数的带上 n/m，例如 枪（4/6）、药（1/1）；无限次的只显示名字（刀、油）
//   - 旁边的 "J" 由你摆一个静态文字进去，用来提示"按 J 使用"
//
// 空手时整个框会隐藏（如果填了 Box Root）。
public class HeldItemHUD : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("手持栏，留空自动在场景里找")]
    public HeldItemSlot slot;

    [Tooltip("图案用的 Image")]
    public Image icon;

    [Tooltip("名字文字，例如 枪（4/6）")]
    public TMP_Text nameLabel;

    [Tooltip("整个框的根物体：空手时整体隐藏。留空则只清空内容，不隐藏")]
    public GameObject boxRoot;

    private InventoryItem lastItem;
    private int lastUses = int.MinValue;

    private void Update()
    {
        if (slot == null)
        {
            slot = FindFirstObjectByType<HeldItemSlot>();
            if (slot == null)
                return;
        }

        InventoryItem item = slot.HeldItem;
        int uses = item != null ? item.remainingUses : 0;

        //大部分时候什么都没变，直接返回；只有换物品或次数变化时才刷新
        if (item == lastItem && uses == lastUses)
            return;

        lastItem = item;
        lastUses = uses;

        Refresh(item);
    }

    private void Refresh(InventoryItem item)
    {
        bool has = item != null && item.data != null;

        if (boxRoot != null)
            boxRoot.SetActive(has);

        if (!has)
        {
            if (nameLabel != null) nameLabel.text = "";
            if (icon != null) icon.enabled = false;
            return;
        }

        //图案：优先用"地上那件道具"的图和颜色；没有 worldPrefab 就退回 ItemData.icon
        Sprite sprite = null;
        Color color = Color.white;

        SpriteRenderer worldRenderer = item.data.worldPrefab != null
            ? item.data.worldPrefab.GetComponentInChildren<SpriteRenderer>(true)
            : null;

        if (worldRenderer != null)
        {
            sprite = worldRenderer.sprite;
            color = worldRenderer.color;
        }
        else if (item.data.icon != null)
        {
            sprite = item.data.icon;
        }

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.color = color;
            icon.enabled = sprite != null;
        }

        if (nameLabel != null)
            nameLabel.text = Format(item);
    }

    //有使用次数的显示"名字（剩/总）"，无限的只显示名字
    private static string Format(InventoryItem item)
    {
        ItemData data = item.data;

        if (data.maxUses >= 0)
            return data.itemName + "（" + item.remainingUses + "/" + data.maxUses + "）";

        return data.itemName;
    }
}
