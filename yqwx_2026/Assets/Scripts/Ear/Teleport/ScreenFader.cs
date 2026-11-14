using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 全屏黑幕：负责"淡出 → 做事 → 淡入"。
//
// 特点：
//   - 自动创建，不需要在场景里摆；DontDestroyOnLoad，切换场景也不会消失
//   - sortingOrder = 9999，盖住所有 UI 和场景
//   - 用 Time.unscaledDeltaTime，所以背包把 timeScale 设成 0 时也能正常淡入淡出
public class ScreenFader : MonoBehaviour
{
    private Image image;//那张铺满屏幕的黑图
    private static ScreenFader instance;

    public static ScreenFader Instance
    {
        get
        {
            if (instance == null) instance = Create();
            return instance;
        }
    }

    private static ScreenFader Create()
    {
        GameObject root = new GameObject("ScreenFader");
        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;//压在所有 UI 上面

        //黑幕不吃鼠标事件：传送靠键盘，不需要它挡点击
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject black = new GameObject("Black");
        black.transform.SetParent(root.transform, false);

        Image img = black.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);//一开始完全透明
        img.raycastTarget = false;

        //铺满整个屏幕
        RectTransform rect = img.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ScreenFader fader = root.AddComponent<ScreenFader>();
        fader.image = img;
        return fader;
    }

    /// <summary>渐变为全黑</summary>
    public IEnumerator FadeOut(float duration) => FadeTo(1f, duration);

    /// <summary>从全黑渐变为透明</summary>
    public IEnumerator FadeIn(float duration) => FadeTo(0f, duration);

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (image == null)
            yield break;

        float startAlpha = image.color.a;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
