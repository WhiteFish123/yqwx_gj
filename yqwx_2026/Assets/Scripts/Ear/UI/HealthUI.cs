using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 血量 UI：把 PlayerHealth 的血量显示成几颗心。
//
// 用法（三步）：
//   1. 在 Canvas 下摆好几个 Image（几颗心就摆几个），Image 的 Source Image 可以留空
//   2. 新建空物体挂上这个脚本，把 Image 按顺序拖进 Hearts 数组（左 → 右）
//   3. 把"满血图"和"空心图"分别拖进 Full Sprite / Empty Sprite —— 图由脚本统一设置
//   Player 留空即可，会自动找场景里的 PlayerHealth
//
// 表现：有血 = 满血图；掉血 = 换成空心图，同时那颗心弹一下。
//       （两张图都没填时，退回"同一张图 + 半透明"的做法）
public class HealthUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家血量组件，留空自动在场景里找")]
    public PlayerHealth player;

    [Tooltip("几颗心，按显示顺序（左 → 右）拖进来")]
    public Image[] hearts;

    [Header("外观：两张图（推荐）")]
    [Tooltip("有血时的图")]
    public Sprite fullSprite;

    [Tooltip("空心时的图")]
    public Sprite emptySprite;

    [Header("外观：没贴图时的退路")]
    [Tooltip("Full Sprite 留空时，有血的心用这个颜色")]
    public Color fullColor = Color.white;

    [Tooltip("Full Sprite 留空时，空心的心用这个颜色（默认半透明）")]
    public Color emptyColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("动画")]
    [Tooltip("掉血时那颗心弹到多大，1 = 不弹")]
    public float popScale = 1.35f;

    [Tooltip("弹一下用多久")]
    public float popTime = 0.12f;

    private int lastHealth = -1;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerHealth>();

        if (player != null)
            player.HealthChanged += OnHealthChanged;

        ApplySprites();

        Refresh(player != null ? player.currentHealth : 0);
    }

    private void Start()
    {
        //保险：Awake 时玩家可能还没把自己的血量初始化好
        if (player != null)
            Refresh(player.currentHealth);
    }

    private void OnDestroy()
    {
        if (player != null)
            player.HealthChanged -= OnHealthChanged;
    }

    //先把满血图铺到所有 Image 上（Refresh 会按实际血量纠正成空心图）
    private void ApplySprites()
    {
        if (fullSprite == null)
            return;

        foreach (Image heart in hearts)
        {
            if (heart != null)
            {
                heart.sprite = fullSprite;
                heart.color = Color.white;
            }
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        int previous = lastHealth;
        Refresh(current);

        //掉血：把刚变空的那颗心弹一下（index = current，因为血是从右往左掉的）
        if (previous > current && current >= 0 && current < hearts.Length && hearts[current] != null)
            StartCoroutine(Pop(hearts[current]));
    }

    private void Refresh(int current)
    {
        lastHealth = current;

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null)
                continue;

            bool hasHealth = i < current;

            if (fullSprite != null)
            {
                //两张图模式：换图，颜色保持白色（图上画成什么色就显示什么色）
                hearts[i].sprite = hasHealth ? fullSprite : (emptySprite != null ? emptySprite : fullSprite);
                hearts[i].color = Color.white;
            }
            else
            {
                //退路：只有一张图时，用颜色区分满/空
                hearts[i].color = hasHealth ? fullColor : emptyColor;
            }
        }
    }

    //弹一下：0 → 1 → 0 的正弦曲线，不受 Time.timeScale 影响
    private IEnumerator Pop(Image heart)
    {
        Vector3 origin = heart.transform.localScale;
        float t = 0f;

        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / popTime) * Mathf.PI);
            heart.transform.localScale = origin * Mathf.Lerp(1f, popScale, k);
            yield return null;
        }

        heart.transform.localScale = origin;
    }
}
