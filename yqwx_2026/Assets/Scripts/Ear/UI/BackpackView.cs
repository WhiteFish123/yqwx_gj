using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 背包界面：6 个固定槽位，每个槽位绑定一件道具。
// 捡到哪件，对应位置的图就出现；丢掉/用掉就消失，下方的数字显示持有数量。
//
// 美术是分层图：Layer 2 是底板，其余每层都是一整张 240x145 的画布，
// 物品已经画在自己该在的位置上，所以每层铺满底板就能精确对齐，
// 不需要在这里写任何坐标。
//
// 键盘：A/D 切换选中的道具，空格正式使用/装备（与双击等价），鼠标悬浮也能看说明。
public class BackpackView : MonoBehaviour
{
    [System.Serializable]
    public class SlotBinding
    {
        [Tooltip("这个槽位对应哪件道具")]
        public ItemData item;

        [Tooltip("这件道具的图（整层铺满底板），拥有时显示")]
        public Image icon;

        [Tooltip("图标下方的数量文字，可留空")]
        public TMP_Text countText;
    }

    [Header("槽位（从左到右：枪、刀、药水、打火机、铁球、油桶）")]
    [SerializeField] private List<SlotBinding> slots = new List<SlotBinding>();

    [Header("设置")]
    [Tooltip("勾上：拿在手上的道具也算拥有，槽位保持显示")]
    [SerializeField] private bool includeHeldItem = true;

    [Tooltip("数量为 1 时也显示数字")]
    [SerializeField] private bool showSingleCount = true;

    [Header("悬浮窗（复用背包的 Tooltip）")]
    [SerializeField] private RectTransform tooltip;
    [SerializeField] private TMP_Text tooltipTitle;
    [SerializeField] private TMP_Text tooltipBody;
    [SerializeField] private CanvasGroup tooltipGroup;

    [Tooltip("浮窗底边相对面板上沿的偏移（0 = 正好贴在背包背景上沿）")]
    [SerializeField] private float tooltipGap;

    [Header("选中高亮（选中项保持原色，其他变暗）")]
    [Tooltip("选中的那一件")]
    [SerializeField] private Color highlightColor = Color.white;

    [Tooltip("其他拥有的道具（压暗）")]
    [SerializeField] private Color dimColor = new Color(0.42f, 0.42f, 0.45f, 1f);

    [Tooltip("选中项的数量文字颜色")]
    [SerializeField] private Color highlightTextColor = Color.white;

    [Tooltip("其他道具的数量文字颜色")]
    [SerializeField] private Color dimTextColor = new Color(0.55f, 0.55f, 0.58f, 1f);

    private Inventory inventory;
    private HeldItemSlot heldItem;
    private UI_Inventory ui;
    private Canvas canvas;
    private bool subscribed;
    private float refreshTimer;

    private int selectedIndex = -1;
    private int hoverIndex = -1;
    private float selectionLostTimer;

    public int SelectedIndex
    {
        get { return selectedIndex; }
    }

    private void Awake()
    {
        Resolve();
    }

    private void OnEnable()
    {
        Resolve();
        hoverIndex = -1;
        Refresh();

        //打开时默认选中第一件拥有的道具
        SelectFirstOwned();
    }

    //装备/喝药会把手上的东西换掉，那一刻背包不一定发通知，所以面板开着时低频补一次
    private void Update()
    {
        HandleKeyboard();

        refreshTimer -= Time.unscaledDeltaTime;

        if (refreshTimer > 0f)
            return;

        refreshTimer = 0.15f;
        Refresh();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void OnDestroy()
    {
        if (inventory != null && subscribed)
        {
            inventory.OnChanged -= Refresh;
            subscribed = false;
        }
    }

    private void Resolve()
    {
        if (inventory == null)
        {
            inventory = FindFirstObjectByType<Inventory>();

            if (inventory != null && !subscribed)
            {
                inventory.OnChanged += Refresh;
                subscribed = true;
            }
        }

        if (heldItem == null)
            heldItem = FindFirstObjectByType<HeldItemSlot>();

        if (ui == null)
            ui = GetComponentInParent<UI_Inventory>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
    }

    //按当前拥有情况刷新 6 个槽位
    public void Refresh()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            SlotBinding slot = slots[i];

            if (slot == null || slot.icon == null)
                continue;

            int count = CountOf(slot.item);
            bool owned = count > 0;

            slot.icon.enabled = owned;

            if (slot.countText != null)
            {
                bool showNumber = owned && (showSingleCount || count > 1);

                slot.countText.enabled = showNumber;
                slot.countText.text = showNumber ? count.ToString() : "";
            }
        }

        //选中的那件被用光了就挪到下一件还拥有的。
        //装备的瞬间“背包里没了、手上还没换上”，会有一两帧读不到，所以给它一点宽限时间。
        if (selectedIndex >= 0 && (selectedIndex >= slots.Count || !IsOwned(selectedIndex)))
        {
            selectionLostTimer += 0.15f;

            if (selectionLostTimer > 0.5f)
            {
                selectionLostTimer = 0f;
                int next = FindNextOwned(selectedIndex, 1);

                if (next < 0)
                    SelectSlot(-1);
                else
                    SelectSlot(next);

                return;
            }
        }
        else
        {
            selectionLostTimer = 0f;
        }

        UpdateSelectionVisuals();

        //浮窗内容跟着走：鼠标停在某个格子上就显示那一格，否则显示当前选中的那一件
        if (hoverIndex >= 0)
            ShowTooltip(hoverIndex);
        else if (selectedIndex >= 0)
            ShowTooltip(selectedIndex);
        else
            HideTooltip();
    }

    //这件道具一共有几个（背包里的堆叠数 + 正拿在手上的）
    public int CountOf(ItemData data)
    {
        if (data == null)
            return 0;

        int total = 0;

        if (inventory != null)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                InventoryItem item = inventory.GetItem(i);

                if (item != null && item.data == data)
                    total += item.stackSize;
            }
        }

        if (includeHeldItem && heldItem != null && heldItem.HeldItem != null && heldItem.HeldItem.data == data)
            total += 1;

        return total;
    }

    //这件道具现在算不算"拥有"（在背包里，或正拿在手上）
    public bool Owns(ItemData data)
    {
        return CountOf(data) > 0;
    }

    private bool IsOwned(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < slots.Count && slots[slotIndex] != null && CountOf(slots[slotIndex].item) > 0;
    }

    //点了某个槽位：武器就装上、血药就喝掉
    public void Activate(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        SlotBinding slot = slots[slotIndex];

        if (slot == null || slot.item == null || inventory == null || ui == null)
            return;

        int index = FindIndex(slot.item);

        if (index < 0)
            return;

        ui.ActivateSlot(index);

        //用完就自动收起背包（空格/双击都走这里）
        ui.SetPanelVisible(false);
    }

    //这件道具在背包的哪一格（不在背包返回 -1）
    public int FindIndex(ItemData data)
    {
        if (inventory == null || data == null)
            return -1;

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            InventoryItem item = inventory.GetItem(i);

            if (item != null && item.data == data)
                return i;
        }

        return -1;
    }

    // ===== 键盘选中 =====

    private void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            MoveSelection(-1);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            MoveSelection(1);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Activate(selectedIndex);
    }

    private void MoveSelection(int step)
    {
        int from = selectedIndex < 0 ? (step > 0 ? -1 : 0) : selectedIndex;
        int next = FindNextOwned(from, step);

        if (next >= 0)
            SelectSlot(next);
    }

    //从 from 开始按 step 方向找下一件拥有的道具（会绕一圈）
    private int FindNextOwned(int from, int step)
    {
        int count = slots.Count;

        if (count == 0)
            return -1;

        for (int i = 1; i <= count; i++)
        {
            int index = ((from + step * i) % count + count) % count;

            if (IsOwned(index))
                return index;
        }

        return -1;
    }

    private void SelectFirstOwned()
    {
        int next = FindNextOwned(-1, 1);

        if (next >= 0)
            SelectSlot(next);
        else
            SelectSlot(-1);
    }

    //选中某个槽位：移选中框 + 显示它的说明
    public void SelectSlot(int slotIndex)
    {
        selectedIndex = slotIndex;

        UpdateSelectionVisuals();

        if (selectedIndex >= 0)
            ShowTooltip(selectedIndex);
        else
            HideTooltip();
    }

    //鼠标进出槽位：进来就先看这一格，离开回到选中项
    public void SetHover(int slotIndex)
    {
        hoverIndex = slotIndex;

        if (hoverIndex >= 0)
            ShowTooltip(hoverIndex);
        else if (selectedIndex >= 0)
            ShowTooltip(selectedIndex);
        else
            HideTooltip();
    }

    // ===== 选中高亮 =====
    // 选中项保持原色，其他拥有的道具压暗；没拥有的本来就是隐藏的，不用管。

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            SlotBinding slot = slots[i];

            if (slot == null || slot.icon == null)
                continue;

            bool selected = i == selectedIndex && slot.icon.enabled;
            Color iconColor = selected ? highlightColor : dimColor;
            iconColor.a = 1f; //图本身的透明度靠 alpha，这里只改颜色

            slot.icon.color = iconColor;

            if (slot.countText != null)
                slot.countText.color = selected ? highlightTextColor : dimTextColor;
        }
    }
    // ===== 悬浮窗 =====

    public void ShowTooltip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        SlotBinding slot = slots[slotIndex];

        if (slot == null || slot.item == null || slot.icon == null || tooltip == null)
            return;

        int count = CountOf(slot.item);

        if (count <= 0) //没捡到的槽位不显示说明
        {
            HideTooltip();
            return;
        }

        if (tooltipTitle != null)
            tooltipTitle.text = slot.item.itemName;

        if (tooltipBody != null)
        {
            string body = slot.item.description;

            int uses = RemainingUsesOf(slot.item);

            if (uses >= 0)
                body += "\n剩余次数：" + uses;

            body += "\n数量：" + count;

            tooltipBody.text = body;
        }

        tooltip.gameObject.SetActive(true);

        //图是整张铺满面板的，所以拿“数量文字”当前排位置；没有就退回图标
        RectTransform anchor = slot.countText != null ? slot.countText.rectTransform : slot.icon.rectTransform;

        PlaceTooltip(anchor);

        SetGroup(tooltipGroup, true);
    }

    //浮窗底边贴在面板上沿，水平对准当前槽位；超出画布就收回画布里
    private void PlaceTooltip(RectTransform anchor)
    {
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        if (canvasRect == null || anchor == null)
            return;

        Vector2 slotLocal = canvasRect.InverseTransformPoint(anchor.position);

        //面板顶边的画布局部高度（面板有 4.8 倍缩放，必须用世界角点换算）
        float panelTopLocal = 0f;
        RectTransform panel = transform as RectTransform;

        if (panel != null)
        {
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            panelTopLocal = canvasRect.InverseTransformPoint(corners[1]).y;
        }

        Vector2 size = tooltip.sizeDelta;
        float halfCanvas = canvasRect.rect.width * 0.5f;
        float halfWidth = size.x * 0.5f;
        float x = Mathf.Clamp(slotLocal.x, -halfCanvas + halfWidth, halfCanvas - halfWidth);

        //锚在底边：y 就是浮窗底边所在高度
        tooltip.anchorMin = new Vector2(0.5f, 0.5f);
        tooltip.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip.pivot = new Vector2(0.5f, 0f);
        tooltip.anchoredPosition = new Vector2(x, panelTopLocal + tooltipGap);
    }

    public void HideTooltip()
    {
        SetGroup(tooltipGroup, false);
    }

    //剩余次数：优先读手上那件，其次读背包里那件；-1 表示没有次数限制
    private int RemainingUsesOf(ItemData data)
    {
        if (data == null || data.maxUses < 0)
            return -1;

        if (heldItem != null && heldItem.HeldItem != null && heldItem.HeldItem.data == data)
            return heldItem.HeldItem.remainingUses;

        int index = FindIndex(data);

        if (index >= 0 && inventory != null && inventory.GetItem(index) != null)
            return inventory.GetItem(index).remainingUses;

        return data.maxUses;
    }

    private static void SetGroup(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = false; //浮窗不挡鼠标，避免指针停在它上面时反复闪烁
        group.interactable = false;
    }
}
