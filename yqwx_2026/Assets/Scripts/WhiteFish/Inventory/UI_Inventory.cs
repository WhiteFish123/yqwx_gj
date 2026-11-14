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
    [SerializeField] private CanvasGroup tooltipGroup;//浮窗透明度控制
    [SerializeField] private CanvasGroup useButtonGroup;//按钮透明度控制

    [Header("手持栏交互")]
    [SerializeField] private KeyCode dropKey = KeyCode.X;//丢弃的快捷键（和界面上的"丢弃"按钮等效）
    [SerializeField] private float dropDistance = 0.8f;//丢到地上时离玩家多远（一格左右）
    [SerializeField] private float dropButtonGap = 6f;//"丢弃"按钮与"装备/使用"按钮之间留多少像素

    [Header("字体")]
    [SerializeField] private TMP_FontAsset fontOverride;//留空则用全局默认

    private Inventory inventory;//背包数据
    private Canvas canvas;//所在画布
    private readonly List<UI_ItemSlot> slots = new List<UI_ItemSlot>();//所有格子
    private int pendingIndex = -1;//等待使用/装备的格子
    private int selectedIndex = -1;//当前点中的格子（丢弃、收回时用；点过一次就一直记着）
    private RectTransform dropButton;//运行时克隆出来的"丢弃"按钮
    private CanvasGroup dropButtonGroup;//它的显隐控制
    private float timeScaleBeforeOpen = 1f;//打开前的时间缩放

    private void Awake()
    {
        //老场景里没有这个字段的值时会读成 None，这里兜一下，保证默认就是 X
        if (dropKey == KeyCode.None) dropKey = KeyCode.X;

        inventory = FindFirstObjectByType<Inventory>();
        canvas = GetComponentInParent<Canvas>();

        GetComponentsInChildren(true, slots);//自动收集所有格子

        ApplyFontToAll();
        HideTooltip();
        HideUseButton();
        CreateDropButton();//克隆一个"丢弃"按钮，贴在"装备/使用"按钮下面

        if (panel != null)
            panel.SetActive(false);

        SetGroup(tooltipGroup, false);//用透明度隐藏，避免 TMP 网格被销毁后重建失败
        SetGroup(useButtonGroup, false);

        ForceUpdateText(gameObject);//预热一次，让隐藏的元素也生成好网格

        if (inventory != null)
            inventory.OnChanged += RefreshAll;
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnChanged -= RefreshAll;

        if (panel != null && panel.activeSelf)//销毁时恢复时间，避免卡在暂停
            Time.timeScale = timeScaleBeforeOpen;
    }

    private void Update()
    {
        if (panel == null)
            return;

        if (Input.GetKeyDown(KeyCode.B))//B 键开关背包
        {
            SetPanelVisible(!panel.activeSelf);
            return;
        }

        if (!panel.activeSelf)//下面这个键只在背包打开时生效
            return;

        if (Input.GetKeyDown(dropKey))
            DropSelected();
    }

    public void SetPanelVisible(bool visible)
    {
        if (panel == null)
            return;

        bool wasVisible = panel.activeSelf;
        panel.SetActive(visible);

        if (visible && !wasVisible)//打开：暂停外界
        {
            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;

            RefreshAll();
        }
        else if (!visible && wasVisible)//关闭：恢复时间
        {
            Time.timeScale = timeScaleBeforeOpen;
        }

        if (!visible)
        {
            HideTooltip();
            HideUseButton();
        }
    }

    private void RefreshAll()//刷新所有格子
    {
        foreach (UI_ItemSlot slot in slots)
            slot.Refresh();
    }

    public void ShowTooltip(UI_ItemSlot slot, Vector2 screenPosition)//显示浮窗
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

            //有使用次数的物品（武器）顺带把耐久显示出来，玩家才知道还能用几次
            if (item.data.maxUses >= 0)
                body += "\n剩余次数：" + item.remainingUses + " / " + item.data.maxUses;

            tooltipBody.text = body;
        }

        PlaceAtPointer(tooltip, screenPosition, 18f);
        SetGroup(tooltipGroup, true);
        ForceUpdateText(tooltip.gameObject);//显示后重建网格
    }

    public void HideTooltip()
    {
        SetGroup(tooltipGroup, false);
    }

    public void ShowUseButton(int index, Vector2 screenPosition)//在鼠标处弹出"使用/装备"按钮
    {
        if (useButton == null)
            return;

        pendingIndex = index;
        selectedIndex = index;//记下来，之后按丢弃/收回键时用
        UpdateButtonLabel(index);//武器显示"装备"，其他显示"使用"

        PlaceAtPointer(useButton, screenPosition, 12f);
        SetGroup(useButtonGroup, true);
        ForceUpdateText(useButton.gameObject);//显示后重建网格

        PlaceDropButton();//"丢弃"紧贴在它下面
        SetGroup(dropButtonGroup, true);
    }

    public void HideUseButton()
    {
        pendingIndex = -1;
        SetGroup(useButtonGroup, false);
        SetGroup(dropButtonGroup, false);//"丢弃"跟着一起藏
    }

    public void UsePendingItem()//按钮点击时执行：武器就装备到手上，其他物品就使用掉
    {
        int index = pendingIndex;

        HideUseButton();
        HideTooltip();

        if (index < 0 || inventory == null)
            return;

        InventoryItem item = inventory.GetItem(index);
        if (item == null)
            return;

        if (IsWeapon(item))
            EquipFromBag(index);
        else
            inventory.UseItem(index);
    }

    // ===== 背包 ↔ 手持栏 ↔ 地上 =====

    //有 heldPrefab 的才算"能拿在手上的东西"（武器/工具）。药草、钥匙这类没有
    private static bool IsWeapon(InventoryItem item)
    {
        return item != null && item.data != null && item.data.heldPrefab != null;
    }

    //按钮文字跟着物品类型变：装备 / 使用
    private void UpdateButtonLabel(int index)
    {
        if (useButton == null)
            return;

        TMP_Text label = useButton.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        InventoryItem item = inventory != null ? inventory.GetItem(index) : null;
        label.text = IsWeapon(item) ? "装备" : "使用";
    }

    //运行时克隆"装备/使用"按钮，得到一个外观、字体完全一致的"丢弃"按钮
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
            button.onClick = new Button.ButtonClickedEvent();//清掉克隆过来的"装备"回调
            button.onClick.AddListener(DropSelected);
        }

        ForceUpdateText(clone);
        SetGroup(dropButtonGroup, false);
    }

    //把"丢弃"按钮摆在"装备/使用"按钮正下方（同宽、同 x）
    private void PlaceDropButton()
    {
        if (dropButton == null || useButton == null)
            return;

        dropButton.sizeDelta = useButton.sizeDelta;
        dropButton.anchoredPosition = useButton.anchoredPosition
                                    - new Vector2(0f, useButton.sizeDelta.y + dropButtonGap);

        //贴到屏幕底部时不要掉出画布
        if (canvas != null)
        {
            float halfHeight = dropButton.sizeDelta.y * 0.5f;
            float minY = -canvas.GetComponent<RectTransform>().sizeDelta.y * 0.5f + halfHeight;

            Vector2 position = dropButton.anchoredPosition;
            if (position.y < minY)
                dropButton.anchoredPosition = new Vector2(position.x, minY);
        }
    }

    //背包 → 手上。手上有东西时先把它收回背包，等价于"交换"
    private bool EquipFromBag(int index)
    {
        if (inventory == null)
            return false;

        HeldItemSlot slot = FindFirstObjectByType<HeldItemSlot>();
        if (slot == null)
            return false;

        //先取出要装备的那件，腾出一个格子，这样旧武器一定放得回去
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

        inventory.AddExistingItem(item);//装不上（比如没有 IUsable）→ 退回背包
        return false;
    }

    //背包 → 地上（按 X 或点界面上的"丢弃"）：丢在玩家面前一格
    public void DropSelected()
    {
        if (inventory == null || selectedIndex < 0)
            return;

        InventoryItem item = inventory.GetItem(selectedIndex);
        if (item == null || item.data == null || item.data.worldPrefab == null)
        {
            Debug.LogWarning("这件物品没有设置 worldPrefab，无法丢到地上", this);
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

    private void PlaceAtPointer(RectTransform target, Vector2 screenPosition, float offset)//放到鼠标旁并夹紧屏幕
    {
        if (canvas == null)
            return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out local))
            return;

        Vector2 size = target.sizeDelta;
        Vector2 canvasSize = canvasRect.sizeDelta;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        float x = local.x + offset + halfW;
        float y = local.y - offset - halfH;

        x = Mathf.Clamp(x, -canvasSize.x * 0.5f + halfW, canvasSize.x * 0.5f - halfW);
        y = Mathf.Clamp(y, -canvasSize.y * 0.5f + halfH, canvasSize.y * 0.5f - halfH);

        target.anchoredPosition = new Vector2(x, y);
    }

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

    private static void SetGroup(CanvasGroup group, bool visible)//用透明度显隐，避免反复销毁重建网格
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }
}
