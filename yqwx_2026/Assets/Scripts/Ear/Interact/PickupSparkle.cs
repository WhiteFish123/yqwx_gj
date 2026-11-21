using UnityEngine;

// 地上待拾取物品的“闪光”表现：循环播放一组 sprite。
// 挂在物品 prefab 的 Sparkle 子物体上，用它替换掉物品本身的样子。
[RequireComponent(typeof(SpriteRenderer))]
public class PickupSparkle : MonoBehaviour
{
    [Tooltip("循环播放的帧（闪光-Sheet_0..5）")]
    public Sprite[] frames;

    [Tooltip("每秒播放多少帧")]
    public float fps = 10f;

    [Tooltip("勾上：每个物品的起始帧随机，地上的闪光不会整齐划一")]
    public bool randomStart = true;

    private SpriteRenderer spriteRenderer;
    private float timer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (randomStart && frames != null && frames.Length > 0)
            timer = Random.Range(0f, frames.Length);
    }

    private void Update()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0)
            return;

        timer += Time.deltaTime * Mathf.Max(1f, fps);

        int index = Mathf.FloorToInt(timer) % frames.Length;

        if (index < 0)
            index += frames.Length;

        spriteRenderer.sprite = frames[index];
    }
}
