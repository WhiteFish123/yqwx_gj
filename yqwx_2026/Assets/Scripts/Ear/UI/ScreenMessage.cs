using System.Collections;
using TMPro;
using UnityEngine;

// 屏幕上的一句话提示（用完自动淡出）。
//
// 用法（要你在场景里搭一次）：
//   1. Canvas 下放一个 TextMeshPro 文字（字体用项目里的中文字体，别用默认的 LiberationSans）
//   2. 给它挂上这个脚本
//   3. 代码里任何地方调：ScreenMessage.Show("我感觉良好，不需要使用这个");
//
// 为什么不用脚本自己创建文字：
//   TMP 默认字体是 LiberationSans，只有拉丁字母，中文会显示成空白。
//   场景里摆一个、用项目自己的中文字体，最稳。
public class ScreenMessage : MonoBehaviour
{
    public static ScreenMessage instance;

    [Tooltip("显示文字的 TMP 组件，留空自动取同物体上的")]
    [SerializeField] private TMP_Text label;

    [Tooltip("文字停留多久（秒）")]
    public float holdTime = 1.2f;

    [Tooltip("淡出用多久（秒）")]
    public float fadeTime = 0.35f;

    private Coroutine routine;

    private void Awake()
    {
        instance = this;

        if (label == null)
            label = GetComponent<TMP_Text>();

        if (label != null)
            label.alpha = 0f;//一开始不可见
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>显示一句话（重复调用会重新计时）</summary>
    public static void Show(string text)
    {
        if (instance == null)
            instance = FindFirstObjectByType<ScreenMessage>(FindObjectsInactive.Include);

        if (instance == null)
        {
            //没搭提示文字也不该崩，只是打个警告
            Debug.LogWarning("ScreenMessage：场景里没有提示文字，消息「" + text + "」没能显示");
            return;
        }

        instance.Play(text);
    }

    private void Play(string text)
    {
        if (label == null)
            return;

        label.text = text;
        label.alpha = 1f;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(FadeRoutine());
    }

    //停留一会儿再淡出。用 unscaledDeltaTime：背包把 timeScale 设成 0 时也能正常消失
    private IEnumerator FadeRoutine()
    {
        float t = 0f;
        while (t < holdTime)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            label.alpha = 1f - Mathf.Clamp01(t / fadeTime);
            yield return null;
        }

        label.alpha = 0f;
        routine = null;
    }
}
