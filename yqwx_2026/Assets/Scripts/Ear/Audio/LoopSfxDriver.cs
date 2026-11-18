using UnityEngine;

// 持续音效驱动器：游戏启动时自动创建，不需要在场景里摆任何东西。
//
// 它负责"什么时候开始 / 什么时候立刻停"两类循环音效：
//   1. 走路声 sfx_walk —— 真的在移动时循环播放，停下（松开方向键 / 撞墙没动）立刻停
//   2. 燃烧声 sfx_burn —— 场上任何一个箱子在燃烧时循环播放，全部熄灭立刻停
//
// 声音本身的循环由 AudioManager.PlayLoopSfx 负责（AudioSource.loop = true），
// 所以在音频播完时会自然重播，直到这里调 StopLoopSfx。
public class LoopSfxDriver : MonoBehaviour
{
    [Header("音效 id（要和 AudioManager 列表里的 id 一致）")]
    public string walkSfxId = "sfx_walk";
    public string burnSfxId = "sfx_burn";

    [Header("判定参数")]
    [Tooltip("跨格之间的空档，给一点宽容时间，避免走路声一顿一顿")]
    public float walkGraceTime = 0.12f;

    [Tooltip("多少秒扫一次场上的箱子，看还有没有在烧")]
    public float chestScanInterval = 0.25f;

    private PlayerBrain brain;
    private float moveTimer;      // 距离"最后一次真的在动"还剩多少宽容时间
    private bool walking;
    private bool burning;
    private float scanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<LoopSfxDriver>() != null)
            return;

        GameObject go = new GameObject("LoopSfxDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<LoopSfxDriver>();
    }

    private void Update()
    {
        UpdateWalkSound();
        UpdateBurnSound();
    }

    // ===== 走路声 =====

    private void UpdateWalkSound()
    {
        if (AudioManager.Instance == null)
            return;

        if (brain == null)
            brain = FindFirstObjectByType<PlayerBrain>();

        //"想走"= 按着方向键且输入没被锁；"真的走"= 引擎说正在跨格
        bool movingNow = brain != null
                         && brain.inputEnabled
                         && brain.moveInput != Vector2Int.zero
                         && brain.motor != null
                         && brain.motor.IsMoving;

        if (movingNow)
            moveTimer = walkGraceTime;
        else
            moveTimer -= Time.unscaledDeltaTime;

        bool shouldPlay = moveTimer > 0f;

        if (shouldPlay && !walking)
        {
            walking = true;
            AudioManager.Instance.PlayLoopSfx(walkSfxId);
        }
        else if (!shouldPlay && walking)
        {
            walking = false;
            AudioManager.Instance.StopLoopSfx(walkSfxId);
        }
    }

    // ===== 燃烧声 =====

    private void UpdateBurnSound()
    {
        if (AudioManager.Instance == null)
            return;

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer > 0f)
            return;

        scanTimer = chestScanInterval;

        bool anyBurning = false;
        Chest[] chests = FindObjectsByType<Chest>(FindObjectsSortMode.None);

        foreach (Chest chest in chests)
        {
            if (chest != null && chest.IsBurning)
            {
                anyBurning = true;
                break;
            }
        }

        if (anyBurning == burning)
            return;

        burning = anyBurning;

        if (burning)
            AudioManager.Instance.PlayLoopSfx(burnSfxId);
        else
            AudioManager.Instance.StopLoopSfx(burnSfxId);//烧完了，立刻停
    }
}
