using UnityEngine;
using UnityEngine.EventSystems;

// 背包槽位上的鼠标交互：
//   悬浮 → 显示名称/说明
//   双击 → 立刻装备（武器）或使用（血药）
// 六张物品图都是整张 240x145 的画布（物品画在各自位置上），
// 所以用 hitArea 把可点区域裁到物品本体，避免六个槽位互相遮挡。
public class BackpackSlotButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
{
    [Tooltip("双击判定窗口（秒）")]
    public float doubleClickWindow = 0.45f;

    [Tooltip("可点击范围：相对整张槽位图的归一化矩形（0~1，y 从底往上）")]
    public Rect hitArea = new Rect(0f, 0f, 1f, 1f);

    public BackpackView view;
    public int slotIndex;

    private float lastClickTime = -10f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (view != null)
            view.SetHover(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (view != null)
            view.SetHover(-1);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (view == null)
            return;

        if (Time.unscaledTime - lastClickTime > doubleClickWindow)
        {
            lastClickTime = Time.unscaledTime;
            return;
        }

        lastClickTime = -10f;
        view.Activate(slotIndex);
    }

    // 只有指针落在 hitArea 里才算点中这个槽位
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform rect = transform as RectTransform;

        if (rect == null)
            return false;

        Vector2 local;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out local))
            return false;

        Rect r = rect.rect;

        if (r.width <= 0f || r.height <= 0f)
            return false;

        float nx = (local.x - r.xMin) / r.width;
        float ny = (local.y - r.yMin) / r.height;

        return nx >= hitArea.xMin && nx <= hitArea.xMax && ny >= hitArea.yMin && ny <= hitArea.yMax;
    }
}
