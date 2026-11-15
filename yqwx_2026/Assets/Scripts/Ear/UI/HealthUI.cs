using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 血量 UI：把 PlayerHealth 的血量显示成几颗心。
//
// 用法（三步）：
//   1. 在 Canvas 下摆好几个 Image（几颗心就摆几个）
//   2. 新建空物体挂上这个脚本，把 Image 按顺序拖进 Hearts 数组（左 → 右）
//   3. Player 留空即可，会自动找场景里的 PlayerHealth
//
// 表现：有血 = 正常颜色；掉血 = 那颗心变成"空心"色（默认半透明），
//       同时弹一下，让玩家注意到自己扣血了。
public class HealthUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家血量组件，留空自动在场景里找")]
    public PlayerHealth player;

    [Tooltip("几颗心，按显示顺序（左 → 右）拖进来")]
    public Image[] hearts;

    [Header("外观")]
    [Tooltip("心形的图。留空就保持 Image 原本的 sprite")]
    public Sprite heartSprite;

    [Tooltip("有血时的颜色")]
    public Color fullColor = Color.white;

    [Tooltip("空心的颜色（默认半透明）")]
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

        //统一换成心形图（如果给了的话）
        if (heartSprite != null)
        {
            foreach (Image heart in hearts)
            {
                if (heart != null)
                    heart.sprite = heartSprite;
            }
        }

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

            hearts[i].color = i < current ? fullColor : emptyColor;
        }
    }

    //弹一下：0 → 1 → 0 的正弦曲线，不收 Time.timeScale 影响
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
