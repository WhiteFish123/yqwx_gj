using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//单个背包格子：显示图标与数量，悬停显示浮窗，点击弹出使用按钮
public class UI_ItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;//图标
    [SerializeField] private TextMeshProUGUI stackText;//数量文本

    private Inventory inventory;//数据来源
    private UI_Inventory ui;//所属界面
    private int index;//格子下标

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
            ui.ShowTooltip(this, eventData.position);
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
            ui.HideUseButton();
        else
            ui.ShowUseButton(index, eventData.position);
    }
}
