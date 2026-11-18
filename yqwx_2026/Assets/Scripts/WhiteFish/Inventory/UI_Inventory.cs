using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//背包界面逻辑：UI 对象都在场景里，这里只负责显隐与交互
public class UI_Inventory : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private GameObject panel;//整个面板（用于显隐）
    [SerializeField] private RectTransform tooltip;//悬停浮窗
    [SerializeField] private TMP_Text tooltipTitle;//浮窗标题
    [SerializeField] private TMP_Text tooltipBody;//浮窗描述
    [SerializeField] private RectTransform useButton;//使用按钮
    [SerializeField] private CanvasGroup tooltipGroup;//浮窗透明度控�?
    [SerializeField] private CanvasGroup useButtonGroup;//按钮透明度控�?

    [Header("\u624B\u6301\u680F\u4EA4\u4E92")]
    [SerializeField] private KeyCode dropKey = KeyCode.X;//drop hotkey, same as the on-screen button
    [SerializeField] private float dropDistance = 0.8f;//丢到地上时离玩家多远（一格左右）
    [SerializeField] private float dropButtonGap = 6f;//"丢弃"按钮�?装备/使用"按钮之间的水平间�?
    [SerializeField] private float actionButtonGap = 8f;//gap between the tooltip bottom and the buttons

    [Header("字体")]
    [SerializeField] private TMP_FontAsset fontOverride;//留空则用全局默认

    [Header("布局")]
    [SerializeField] private float tooltipGap = 8f;//浮窗与格子之间的空隙（像素）
    [SerializeField] private float tooltipShiftLeft = 0f;//浮窗整体左移量（像素），0 表示紧贴格子右侧不遮�?

    private Inventory inventory;//背包数据
    private Canvas canvas;//所在画�?
    private readonly List<UI_ItemSlot> slots = new List<UI_ItemSlot>();//所有格�?
    private int pendingIndex = -1;//等待使用/装备的格�?
    private int selectedIndex = -1;//当前点中的格子（丢弃、收回时用；点过一次就一直记着�?
    private RectTransform dropButton;//运行时克隆出来的"丢弃"按钮
    private CanvasGroup dropButtonGroup;//它的显隐控制
    private float timeScaleBeforeOpen = 1f;//打开前的时间缩放

    private void Awake()
    {
        //老场景里没有这个字段的值时会读�?None，这里兜一下，保证默认就是 X
        if (dropKey == KeyCode.None) dropKey = KeyCode.X;

        inventory = FindFirstObjectByType<Inventory>();
        canvas = GetComponentInParent<Canvas>();

        GetComponentsInChildren(true, slots);//自动收集所有格�?

        ApplyFontToAll();
        HideTooltip();
        HideUseButton();
        CreateDropButton();//克隆一�?丢弃"按钮，贴�?装备/使用"按钮下面

        if (panel != null)
            panel.SetActive(false);

        SetGroup(tooltipGroup, false);//用透明度隐藏，避免 TMP 网格被销毁后重建失败
        SetGroup(useButtonGroup, false);

        ForceUpdateText(gameObject);//预热一次，让隐藏的元素也生成好网格

        if (inventory != null)
            inventory.OnChanged += RefreshAll;

        ReparentActionButtons();//the buttons must not live under Tooltip, or its CanvasGroup hides them too
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnChanged -= RefreshAll;

        if (panel != null && panel.activeSelf)//销毁时恢复时间，避免卡在暂�?
            if (!IsOtherPauseActive()) Time.timeScale = 1f;//do not carry paused state into the next scene
    }

    private void Update()
    {
        if (panel == null)
            return;

        if (Input.GetKeyDown(KeyCode.B))//B 键开关背�?
        {
            SetPanelVisible(!panel.activeSelf);
            return;
        }


        RecoverStuckTimeScale();//no pause source but time is frozen: rescue it
        if (!panel.activeSelf)//下面这个键只在背包打开时生�?
            return;

        HideTooltipIfPointerOnIt();//鼠标压到浮窗上就收起，避免浮窗挡住操�?

        if (Input.GetKeyDown(dropKey))
            DropSelected();
    }

    //鼠标进入浮窗覆盖的区域时收起浮窗�?
    //只靠格子�?PointerExit 不够：浮窗左移后会盖住格子的一部分�?
    //鼠标移到那部分上时仍�?在格子里"，浮窗就会一直显示�?
    private void HideTooltipIfPointerOnIt()
    {
        if (tooltip == null || tooltipGroup == null || tooltipGroup.alpha <= 0f)
            return;

        Camera camera = null;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = canvas.worldCamera;

        if (RectTransformUtility.RectangleContainsScreenPoint(tooltip, Input.mousePosition, camera))
            HideTooltip();
    }

    public void SetPanelVisible(bool visible)
    {
        if (panel == null)
            return;

        bool wasVisible = panel.activeSelf;
        panel.SetActive(visible);

        if (visible && !wasVisible)//打开：暂停外�?
        {
            timeScaleBeforeOpen = Time.timeScale > 0f ? Time.timeScale : 1f;//guard: never snapshot 0
            Time.timeScale = 0f;

            RefreshAll();
        }
        else if (!visible && wasVisible)//关闭：恢复时�?
        {
            Time.timeScale = IsOtherPauseActive() ? 0f : timeScaleBeforeOpen;//keep frozen if pause menu is still open
        }

        if (!visible)
        {
            HideTooltip();
            HideUseButton();
        }
    }

    //Is there another pause source (the pause menu) currently holding the game frozen?
    private static bool IsOtherPauseActive()
    {
        PauseMenu menu = FindFirstObjectByType<PauseMenu>();
        return menu != null && menu.IsPaused;
    }

    //If the inventory is closed, no pause source is active, but time is still frozen,
    //restore it. This covers the case where the pause menu restored a stale 0 snapshot.
    private void RecoverStuckTimeScale()
    {
        if (panel == null || panel.activeSelf)
            return;

        if (Time.timeScale > 0f || IsOtherPauseActive())
            return;

        Time.timeScale = 1f;
    }


    //Move the action buttons out from under Tooltip: Tooltip owns a CanvasGroup whose alpha/raycast
    //settings cascade to children, so a button parented there becomes invisible and unclickable.
    private void ReparentActionButtons()
    {
        if (useButton == null || tooltip == null)
            return;

        if (!useButton.IsChildOf(tooltip))
            return;

        Transform target = canvas != null ? canvas.transform : transform;
        useButton.SetParent(target, true);//keep world position so the authoring layout is preserved

        if (dropButton != null)
            dropButton.SetParent(target, true);
    }

    //Place the use/drop buttons directly under the tooltip so they follow it.
    private void PlaceActionButtons()
    {
        if (useButton == null)
            return;

        if (tooltip != null && tooltip.gameObject.activeSelf && useButton.parent != null)
        {
            Vector3[] corners = new Vector3[4];
            tooltip.GetWorldCorners(corners);//0=���� 1=���� 2=���� 3=����

            Vector3 local = useButton.parent.InverseTransformPoint(corners[0]);//tooltip bottom-left
            useButton.anchoredPosition = new Vector2(local.x, local.y - actionButtonGap);
        }

        if (dropButton != null)
            dropButton.anchoredPosition = useButton.anchoredPosition
                + new Vector2(useButton.sizeDelta.x + dropButtonGap, 0f);
    }

    private void RefreshAll()//刷新所有格�?
    {
        foreach (UI_ItemSlot slot in slots)
            slot.Refresh();
    }

    public void ShowTooltip(UI_ItemSlot slot)//显示浮窗，贴在格子旁�?
    {
        InventoryItem item = slot.CurrentItem();

        if (item == null || item.data == null || tooltip == null)
        {
            HideTooltip();
            return;
        }

        if (tooltipTitle != null)
            tooltipTitle.text = item.data.itemName;

        if (tooltipBody != null)
        {
            string body = item.data.description;

            //有使用次数的物品（武器）顺带把耐久显示出来，玩家才知道还能用几�?
            if (item.data.maxUses >= 0)
                body += "\n\u5269\u4F59\u6B21\u6570\uFF1A" + item.remainingUses + " / " + item.data.maxUses;

            tooltipBody.text = body;
        }

        RectTransform slotRect = slot.transform as RectTransform;

        tooltip.gameObject.SetActive(true);//先按最终内容量出真实尺寸，再定�?
        ForceUpdateText(tooltip.gameObject);

        PlaceBesideSlot(tooltip, slotRect, tooltipGap, tooltipShiftLeft);
        SetGroup(tooltipGroup, true);
    }

    public void HideTooltip()
    {
        SetGroup(tooltipGroup, false);
    }

    public void ShowUseButton(int index, UI_ItemSlot slot)//在提示栏里显�?使用/装备"�?丢弃"
    {
        if (useButton == null)
            return;

        pendingIndex = index;
        selectedIndex = index;//记下来，之后按丢弃键时用

        SlotAction action = ActionFor(inventory != null ? inventory.GetItem(index) : null);

        if (action == SlotAction.None)
        {
            //�?�?这种既不能装备、也不是消耗品的物品：只给一�?丢弃"
            SetGroup(useButtonGroup, false);
        }
        else
        {
            UpdateButtonLabel(index);//装备 / 使用
            SetGroup(useButtonGroup, true);
            ForceUpdateText(useButton.gameObject);//显示后重建网�?
        }

        PlaceActionButtons();//stick both buttons right under the tooltip
        SetGroup(dropButtonGroup, true);
    }

    public void HideUseButton()
    {
        pendingIndex = -1;
        selectedIndex = -1;//clear it, otherwise a later drop key would hit a different item
        SetGroup(useButtonGroup, false);
        SetGroup(dropButtonGroup, false);//"丢弃"跟着一起藏
    }

    //双击格子的快捷使用：装备的走装备，消耗品用掉一�?
    public void ActivateSlot(int index)
    {
        if (inventory == null)
            return;

        InventoryItem item = inventory.GetItem(index);
        if (item == null)
            return;

        HideUseButton();
        HideTooltip();

        ExecuteAction(index, item);
    }

    public void UsePendingItem()//button click handler
    {
        int index = pendingIndex;

        HideUseButton();
        HideTooltip();

        if (index < 0 || inventory == null)
            return;

        InventoryItem item = inventory.GetItem(index);
        if (item == null)
            return;

        ExecuteAction(index, item);
    }

    //Both entry points share this: equip or consume, chosen by the item data
    private void ExecuteAction(int index, InventoryItem item)
    {
        switch (ActionFor(item))
        {
            case SlotAction.Equip:
                EquipFromBag(index);
                break;

            case SlotAction.Use:
                inventory.UseItem(index);
                break;
        }
    }

    // ===== 背包 �?手持�?�?地上 =====

    //点开一个格子之后能做什�?

    private static SlotAction ActionFor(InventoryItem item) => InventoryActions.ActionFor(item);//pure rule lives in InventoryActions

    //按钮文字跟着物品类型变：装备 / 使用
    private void UpdateButtonLabel(int index)
    {
        if (useButton == null)
            return;

        TMP_Text label = useButton.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        InventoryItem item = inventory != null ? inventory.GetItem(index) : null;
        label.text = ActionFor(item) == SlotAction.Equip ? "装备" : "使用";
    }

    //运行时克�?装备/使用"按钮，得到一个外观、字体完全一致的"丢弃"按钮（并排放在它右边�?
    //（这样你不需要在场景里再拖一个按钮，也不会出现两套样式不一致）
    private void CreateDropButton()
    {
        if (useButton == null)
            return;

        GameObject clone = Instantiate(useButton.gameObject, useButton.parent);
        clone.name = "DropButton";

        dropButton = clone.GetComponent<RectTransform>();

        dropButtonGroup = clone.GetComponent<CanvasGroup>();
        if (dropButtonGroup == null)
            dropButtonGroup = clone.AddComponent<CanvasGroup>();

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "丢弃";

        Button button = clone.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();//清掉克隆过来�?装备"回调
            button.onClick.AddListener(DropSelected);
        }

        dropButton.anchoredPosition = useButton.anchoredPosition
                                    + new Vector2(useButton.sizeDelta.x + dropButtonGap, 0f);//并排在右�?

        ForceUpdateText(clone);
        SetGroup(dropButtonGroup, false);
    }

    //背包 �?手上。手上有东西时先把它收回背包，等价于"交换"
    private bool EquipFromBag(int index)
    {
        if (inventory == null)
            return false;

        HeldItemSlot slot = FindFirstObjectByType<HeldItemSlot>();
        if (slot == null)
            return false;

        //先取出要装备的那件，腾出一个格子，这样旧武器一定放得回�?
        InventoryItem item = inventory.RemoveItem(index);
        if (item == null)
            return false;

        if (slot.HasItem)
        {
            InventoryItem back = slot.Unequip();
            if (back != null)
                inventory.AddExistingItem(back);
        }

        if (slot.Equip(item))
            return true;

        inventory.AddExistingItem(item);//装不上（比如没有 IUsable）→ 退回背�?
        return false;
    }

    //背包 �?地上（按 X 或点界面上的"丢弃"）：丢在玩家面前一�?
    public void DropSelected()
    {
        if (inventory == null || selectedIndex < 0)
            return;

        InventoryItem item = inventory.GetItem(selectedIndex);
        if (item == null || item.data == null || item.data.worldPrefab == null)
        {
            Debug.LogWarning("\u8FD9\u4EF6\u7269\u54C1\u6CA1\u6709\u8BBE\u7F6E worldPrefab\uFF0C\u65E0\u6CD5\u4E22\u5230\u5730\u4E0A", this);
            return;
        }

        HeldItemSlot slot = FindFirstObjectByType<HeldItemSlot>();

        Vector3 position = slot != null ? slot.transform.position : Vector3.zero;

        PlayerGridMove motor = slot != null ? slot.GetComponent<PlayerGridMove>() : null;
        Vector3 facing = motor != null
            ? new Vector3(motor.Facing.x, motor.Facing.y, 0f)
            : Vector3.down;

        position += facing * dropDistance;

        inventory.RemoveItem(selectedIndex);
        Pickup.Spawn(item.data, item.remainingUses, position);//次数一起带出去

        HideUseButton();//丢完把按钮收起来，避免误点第二次
        HideTooltip();
    }

    //�?target 摆在格子旁边：优先右侧，右边放不下就放左侧；垂直居中于格子；shiftLeft 让它再往左挪一�?

    private void ApplyFontToAll()//统一字体
    {
        TMP_FontAsset resolved = fontOverride != null ? fontOverride : TMP_Settings.defaultFontAsset;
        if (resolved == null)
            return;

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            text.font = resolved;
    }

    private static void ForceUpdateText(GameObject root)//激活后重建文本网格
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            text.ForceMeshUpdate(true, true);
    }

    private static void SetGroup(CanvasGroup group, bool visible)//用透明度显隐，避免反复销毁重建网�?
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    private void PlaceBesideSlot(RectTransform target, RectTransform slotRect, float gap, float shiftLeft)
    {
        if (canvas == null)
            return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        TooltipLayout.PlaceBeside(canvasRect, cam, target, slotRect, gap, shiftLeft);//pure layout math lives there
    }

}





