using UnityEngine;
using UnityEngine.UI;

//把一组精灵按固定帧率循环播放（用于开场黑幕中间的车灯特写，不依赖 Animator / Controller）
//流程：先保持全透明 startDelay 秒 → 在 fadeInDuration 秒内淡入 → 播放一遍 → 停在最后一帧 cycleHold 秒 → 再播下一遍
[RequireComponent(typeof(Image))]
public class SpriteLoopPlayer : MonoBehaviour
{
    [Header("帧")]
    [Tooltip("要播放的帧，按顺序播放")]
    public Sprite[] frames;

    [Tooltip("每秒播放多少帧（越小越慢）")]
    public float fps = 8f;

    [Header("节奏")]
    [Tooltip("进入场景后先保持全透明多少秒")]
    public float startDelay = 3f;

    [Tooltip("淡入需要多少秒（0 = 直接出现）")]
    public float fadeInDuration = 1.5f;

    [Tooltip("一遍播完后停在最后一帧多少秒，再循环下一遍")]
    public float cycleHold = 2f;

    [Tooltip("关掉则只播一遍，停在最后一帧")]
    public bool loop = true;

    private Image image;
    private float elapsed;
    private int index = -1;

    private void Awake()
    {
        image = GetComponent<Image>();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        elapsed = 0f;
        index = -1;
        SetAlpha(0f);
        Apply();
    }

    private void Update()
    {
        if (image == null || frames == null || frames.Length == 0)
            return;

        elapsed += Time.unscaledDeltaTime;

        //开场先黑着
        if (elapsed < startDelay)
        {
            SetAlpha(0f);
            return;
        }

        float t = elapsed - startDelay;

        //淡入
        float alpha = fadeInDuration <= 0.01f ? 1f : Mathf.Clamp01(t / fadeInDuration);
        SetAlpha(alpha);

        float step = 1f / Mathf.Max(1f, fps);
        float playTime = frames.Length * step;
        float cycle = loop ? playTime + Mathf.Max(0f, cycleHold) : playTime;

        //一遍播完后的等待时间也计入周期，用 Repeat 回到开头
        float local = cycle > 0f ? Mathf.Repeat(t, cycle) : 0f;
        int wanted = Mathf.Clamp(Mathf.FloorToInt(local / step), 0, frames.Length - 1);

        if (wanted != index)
        {
            index = wanted;
            Apply();
        }
    }

    private void Apply()
    {
        if (image == null || frames == null || frames.Length == 0)
            return;

        image.sprite = frames[Mathf.Clamp(index < 0 ? 0 : index, 0, frames.Length - 1)];
    }

    private void SetAlpha(float alpha)
    {
        if (image == null)
            return;

        var c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
