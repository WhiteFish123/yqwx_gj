using UnityEngine;

//提示框/按钮的定位计算：贴在格子旁边，优先放右侧，右边放不下就翻到左侧，
//垂直方向与格子居中并夹在画布内。抽成纯静态类，方便单独验证与复用。
public static class TooltipLayout
{
    private static readonly Vector3[] corners = new Vector3[4];//复用缓冲，避免每次悬停都 new

    //把 target 摆到 slotRect 旁边。返回 false 表示参数不完整，没有移动。
    public static bool PlaceBeside(RectTransform canvasRect, Camera cam, RectTransform target,
        RectTransform slotRect, float gap, float shiftLeft)
    {
        if (canvasRect == null || target == null || slotRect == null)
            return false;

        slotRect.GetWorldCorners(corners);//0=左下 1=左上 2=右上 3=右下

        Vector2 slotMin;
        Vector2 slotMax;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, RectTransformUtility.WorldToScreenPoint(cam, corners[0]), cam, out slotMin))
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, RectTransformUtility.WorldToScreenPoint(cam, corners[2]), cam, out slotMax))
            return false;

        Vector2 size = target.rect.size;
        Vector2 pivot = target.pivot;//浮窗的 pivot 在左上角，位置要按左边缘算
        Vector2 canvasSize = canvasRect.sizeDelta;

        float left = slotMax.x + gap - shiftLeft;//元素左边沿
        if (left + size.x > canvasSize.x * 0.5f)//右边放不下就翻到左边
            left = slotMin.x - gap - size.x;

        float top = (slotMin.y + slotMax.y) * 0.5f + size.y * 0.5f;//元素顶边（与格子垂直居中）

        float clampedLeft = Mathf.Clamp(left, -canvasSize.x * 0.5f, canvasSize.x * 0.5f - size.x);
        float clampedTop = Mathf.Clamp(top, -canvasSize.y * 0.5f + size.y, canvasSize.y * 0.5f);

        target.anchoredPosition = new Vector2(
            clampedLeft + pivot.x * size.x,
            clampedTop - (1f - pivot.y) * size.y);

        return true;
    }
}
