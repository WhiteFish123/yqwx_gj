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

    [Header("字体")]
    [SerializeField] private TMP_FontAsset fontOverride;//留空则用全局默认

    private Inventory inventory;//背包数据
    private Canvas canvas;//所在画布
    private readonly List<UI_ItemSlot> slots = new List<UI_ItemSlot>();//所有格子
    private int pendingIndex = -1;//等待使用的格子
    private float timeScaleBeforeOpen = 1f;//打开前的时间缩放

    private void Awake()
    {
        inventory = FindFirstObjectByType<Inventory>();
        canvas = GetComponentInParent<Canvas>();

        GetComponentsInChildren(true, slots);//自动收集所有格子

        ApplyFontToAll();
        HideTooltip();
        HideUseButton();

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
            SetPanelVisible(!panel.activeSelf);
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
            tooltipBody.text = item.data.description;

        PlaceAtPointer(tooltip, screenPosition, 18f);
        SetGroup(tooltipGroup, true);
        ForceUpdateText(tooltip.gameObject);//显示后重建网格
    }

    public void HideTooltip()
    {
        SetGroup(tooltipGroup, false);
    }

    public void ShowUseButton(int index, Vector2 screenPosition)//在鼠标处弹出使用按钮
    {
        if (useButton == null)
            return;

        pendingIndex = index;
        PlaceAtPointer(useButton, screenPosition, 12f);
        SetGroup(useButtonGroup, true);
        ForceUpdateText(useButton.gameObject);//显示后重建网格
    }

    public void HideUseButton()
    {
        pendingIndex = -1;
        SetGroup(useButtonGroup, false);
    }

    public void UsePendingItem()//按钮点击时执行
    {
        if (pendingIndex < 0 || inventory == null)
        {
            HideUseButton();
            return;
        }

        inventory.UseItem(pendingIndex);
        HideUseButton();
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
