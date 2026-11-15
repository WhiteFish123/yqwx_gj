using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 右下角的"手上道具"提示框。
//
// 显示内容：
//   - 地上那件道具的图案（直接取 worldPrefab 上 SpriteRenderer 的 sprite 和颜色）
//   - 名字；有使用次数的带上 n/m，例如 枪（4/6）、药（1/1）；无限次的只显示名字（刀、油）
//   - 旁边的 "J" 由你摆一个静态文字进去，用来提示"按 J 使用"
//
// ★ 空手时用 CanvasGroup.alpha = 0 隐藏，而不是 SetActive(false)。
//   原因：这个脚本常常就挂在"要被隐藏的那个框"上，
//   一旦 SetActive(false) 把自己也关掉，Update 就停了，永远没机会再显示出来。
public class HeldItemHUD : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("手持栏，留空自动在场景里找")]
    public HeldItemSlot slot;

    [Tooltip("图案用的 Image")]
    public Image icon;

    [Tooltip("名字文字，例如 枪（4/6）")]
    public TMP_Text nameLabel;

    [Tooltip("要显示/隐藏的那个框。可以填脚本所在的物体自身；留空则始终显示，只清空内容")]
    public GameObject boxRoot;

    private InventoryItem lastItem;
    private int lastUses = int.MinValue;
    private CanvasGroup boxGroup;

    private void Awake()
    {
        //给 boxRoot 加（或取）一个 CanvasGroup，用它来控制显隐
        if (boxRoot != null)
        {
            boxGroup = boxRoot.GetComponent<CanvasGroup>();
            if (boxGroup == null)
                boxGroup = boxRoot.AddComponent<CanvasGroup>();
        }
    }

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

        SetVisible(has);

        if (!has)
        {
            if (nameLabel != null) nameLabel.text = "";
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

    //显隐：有 CanvasGroup 就调透明度（物体保持激活，脚本能继续跑）
    private void SetVisible(bool visible)
    {
        if (boxGroup != null)
        {
            boxGroup.alpha = visible ? 1f : 0f;
            boxGroup.blocksRaycasts = visible;
            boxGroup.interactable = visible;
            return;
        }

        //退路：没有 boxRoot 就不隐藏（只清空内容）
        if (boxRoot != null && boxRoot != gameObject)
            boxRoot.SetActive(visible);
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
