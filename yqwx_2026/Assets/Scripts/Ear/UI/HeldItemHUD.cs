using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

// 右下角的"手上道具"提示框。
//
// 显示内容：
//   - 地上那件道具的图案（直接取 worldPrefab 上 SpriteRenderer 的 sprite 和颜色）
//   - 名字；有使用次数的带上 n/m，例如 枪（4/6）、药（1/1）；无限次的只显示名字（刀、油）
//   - 旁边的 "J" 由你摆一个静态文字进去，用来提示"按 J 使用"
//
// 对话时：整框（图标/名字/按键提示）一起淡出，对话结束后自动恢复。
// 判断依据是 DialogueRunner.IsDialogueRunning，所以不管对话是快捷键开的还是 NPC 开的都生效。
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

    [Header("对话")]
    [Tooltip("对话进行时把整个框（图标 + 名字 + 按键提示）淡出，对话结束后恢复")]
    public bool hideDuringDialogue = true;

    [Tooltip("对话系统，留空自动在场景里找（对话是谁开的都算，不只 DialogueHotkey）")]
    public DialogueRunner dialogueRunner;

    [Header("图案尺寸")]
    [Tooltip("打开后：按图片比例自动调整图案框大小，细长道具会占更多横向空间。关掉则固定框大小、只等比缩放图片")]
    public bool fitIconToSprite = false;

    [Tooltip("图案框的高度（像素）")]
    public float iconHeight = 56f;

    [Tooltip("最宽能到高度的几倍，超过就等比缩小，免得挤到名字文字")]
    public float iconMaxWidthRatio = 1.5f;

    private InventoryItem lastItem;
    private int lastUses = int.MinValue;
    private CanvasGroup boxGroup;
    private bool dialogueHidden;      //对话正开着 → 整框淡出

    private Vector2 iconCenter;        //图案框初始的视觉中心（相对父物体）
    private bool iconCenterCached;

    private void Awake()
    {
        //给 boxRoot 加（或取）一个 CanvasGroup，用它来控制显隐
        if (boxRoot != null)
        {
            boxGroup = boxRoot.GetComponent<CanvasGroup>();
            if (boxGroup == null)
                boxGroup = boxRoot.AddComponent<CanvasGroup>();
        }

        if (dialogueRunner == null)
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        //记下图案框原本的中心，之后按比例改尺寸时用它把位置补偿回来
        CacheIconCenter();
    }

    private void Update()
    {
        //对话开着就整框淡出，对话结束再恢复（每帧比对一次 bool，不依赖对话是从哪开的）
        UpdateDialogueHidden();

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

    //对话状态变化时才动一次显隐：
    //对话开始 → 整个框（图标/名字/按键提示）一起淡出；对话结束 → 按手上有没有东西恢复。
    private void UpdateDialogueHidden()
    {
        if (dialogueRunner == null)
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        bool hiddenNow = hideDuringDialogue
                         && dialogueRunner != null
                         && dialogueRunner.IsDialogueRunning;

        if (hiddenNow == dialogueHidden)
            return;

        dialogueHidden = hiddenNow;
        ApplyVisibility();
    }

    //显隐的唯一口径：有物品 且 不在对话中
    private void ApplyVisibility()
    {
        bool has = lastItem != null && lastItem.data != null;
        SetVisible(has && !dialogueHidden);
    }

    private void Refresh(InventoryItem item)
    {
        bool has = item != null && item.data != null;

        SetVisible(has && !dialogueHidden);

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

            //★ 关键：保持图片原始比例。
            //  不勾的话，方形格子里的细长道具（刀、钥匙）会被拉伸压扁。
            //  代码里强制设一次，免得忘了在 Inspector 勾。
            icon.preserveAspect = true;

            if (sprite != null && fitIconToSprite)
                FitIcon(sprite);
        }

        if (nameLabel != null)
            nameLabel.text = Format(item);
    }

    //按图片比例调整图案框的大小：高度固定，宽度 = 高度 × 宽高比，
    //太宽的图会等比缩小到 maxWidthRatio 倍以内，避免挤到名字文字。
    //
    //★ 改尺寸时必须补偿位置：
    //  图案框的 Pivot 通常设在左边缘，宽度一变就是"从左边往右长"，
    //  中心会跟着右移，看起来就偏离竖直轴心。这里把视觉中心钉回原位。
    private void FitIcon(Sprite sprite)
    {
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);

        float height = iconHeight;
        float width = height * aspect;

        float maxWidth = height * iconMaxWidthRatio;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = maxWidth / aspect;
        }

        RectTransform rect = icon.rectTransform;
        rect.sizeDelta = new Vector2(width, height);

        //视觉中心 = 锚点位置 + (0.5 - pivot) * 尺寸；把中心拉回初始位置
        Vector2 pos = rect.anchoredPosition;
        pos.x = iconCenter.x - (0.5f - rect.pivot.x) * width;
        pos.y = iconCenter.y - (0.5f - rect.pivot.y) * height;
        rect.anchoredPosition = pos;
    }

    //记下图案框初始的视觉中心（只在第一次用到时算一次）
    private void CacheIconCenter()
    {
        if (icon == null || iconCenterCached)
            return;

        RectTransform rect = icon.rectTransform;

        float width = rect.rect.width > 0f ? rect.rect.width : rect.sizeDelta.x;
        float height = rect.rect.height > 0f ? rect.rect.height : rect.sizeDelta.y;

        iconCenter.x = rect.anchoredPosition.x + (0.5f - rect.pivot.x) * width;
        iconCenter.y = rect.anchoredPosition.y + (0.5f - rect.pivot.y) * height;

        iconCenterCached = true;
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

    //有使用次数的显示"名字（剩/总）"，无限的只显示名字；弹药无上限的枪只显示剩余发数
    private static string Format(InventoryItem item)
    {
        ItemData data = item.data;

        if (data.ammoNoLimit)
            return data.itemName + "（" + Mathf.Max(0, item.remainingUses) + "）";

        if (data.maxUses >= 0)
            return data.itemName + "（" + item.remainingUses + "/" + data.maxUses + "）";

        return data.itemName;
    }
}
