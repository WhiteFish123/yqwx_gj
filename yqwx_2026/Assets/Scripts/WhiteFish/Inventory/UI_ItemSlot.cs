using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//单个背包格子：显示图标与数量，悬停显示浮窗，点击弹出使用按钮
public class UI_ItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;//图标
    [SerializeField] private TextMeshProUGUI stackText;//数量文本
    [SerializeField] private float doubleClickWindow = 0.3f;//双击判定窗口（秒）

    private Inventory inventory;//数据来源
    private UI_Inventory ui;//所属界面
    private int index;//格子下标
    private float lastClickTime = -10f;//上一次点击时间（不受 timeScale 影响）

    private void Awake()
    {
        ui = GetComponentInParent<UI_Inventory>();
        inventory = FindFirstObjectByType<Inventory>();
        index = transform.GetSiblingIndex();//按层级顺序当作格子编号
    }

    public void Refresh()//根据数据刷新显示
    {
        InventoryItem item = CurrentItem();

        if (item == null)
        {
            if (icon != null)
                icon.enabled = false;

            if (stackText != null)
                stackText.text = "";

            return;
        }

        if (icon != null)
        {
            icon.enabled = true;
            icon.sprite = item.data.icon;
            icon.color = item.data.icon == null ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        if (stackText != null)
            stackText.text = item.stackSize > 1 ? item.stackSize.ToString() : "";
    }

    public InventoryItem CurrentItem()
    {
        return inventory == null ? null : inventory.GetItem(index);
    }

    public void OnPointerEnter(PointerEventData eventData)//悬停
    {
        if (ui != null)
            ui.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)//移出
    {
        if (ui != null)
            ui.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)//点击
    {
        if (ui == null)
            return;

        if (CurrentItem() == null)
        {
            ui.HideUseButton();
            return;
        }

        //快速双击：直接用掉 / 装备（背包打开时 timeScale=0，只能用 unscaledTime）
        if (Time.unscaledTime - lastClickTime <= doubleClickWindow)
        {
            lastClickTime = -10f;
            ui.ActivateSlot(index);
            return;
        }

        lastClickTime = Time.unscaledTime;
        ui.ShowUseButton(index, this);
    }
}
