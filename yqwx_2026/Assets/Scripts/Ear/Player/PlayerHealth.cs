using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 玩家血量：3 滴血、受伤闪红、1 秒无敌、Boss 一击必杀。
//
// 谁会打我：
//   - 史莱姆 / 石像的冲刺撞到（Enemy_AttackState、Enemy_StoneChargeState 里调 Hurt）
//   - 燃烧的箱子（BurningChestHazard 每帧检查）
//   - Boss（以后让它发一个 HitType.boss 的伤害，这里直接清空血量）
//
// 为什么伤害走 Hurt(int) 而不是只走 TakeHit：
//   TakeHit 是给"刀枪子弹"这类有 HitInfo 的东西用的（要区分伤害类型）；
//   敌人的碰撞、火焰这种"环境伤害"只有点数没有类型，直接调 Hurt 更直白。
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("血量")]
    public int maxHealth = 3;
    public int currentHealth;

    [Header("受伤表现")]
    [Tooltip("受伤后的无敌时间（秒）")]
    public float invincibleTime = 1f;

    [Tooltip("闪红的颜色")]
    public Color hurtColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("闪红持续多久")]
    public float flashTime = 0.12f;

    [Tooltip("无敌期间闪烁的间隔")]
    public float blinkInterval = 0.08f;

    [Header("死亡")]
    [Tooltip("死亡后是否重开当前场景")]
    public bool reloadSceneOnDeath = true;

    [Tooltip("死亡淡出时长")]
    public float deathFadeTime = 0.6f;

    public bool IsDead { get; private set; }
    public bool IsInvincible => invincibleTimer > 0f;

    /// <summary>血量变化时通知（当前血量, 最大血量）—— 以后接 UI 血条用</summary>
    public event Action<int, int> HealthChanged;

    private SpriteRenderer spriteRenderer;
    private PlayerBrain brain;
    private float invincibleTimer;
    private Color normalColor = Color.white;

    private void Awake()
    {
        currentHealth = maxHealth;

        spriteRenderer = GetComponent<SpriteRenderer>();
        brain = GetComponent<PlayerBrain>();

        if (spriteRenderer != null)
            normalColor = spriteRenderer.color;
    }

    private void Update()
    {
        if (invincibleTimer > 0f)
            invincibleTimer -= Time.deltaTime;
    }

    // ===== IDamageable：刀枪子弹这类带类型的伤害 =====

    public void TakeHit(HitInfo info)
    {
        if (IsDead) return;

        //Boss 一击必杀：直接把剩下的血全扣掉
        if (info.type == HitType.boss)
        {
            Hurt(currentHealth);
            return;
        }

        Hurt(Mathf.Max(1, Mathf.RoundToInt(info.damage)));
    }

    // ===== 通用入口：敌人碰撞、火焰等环境伤害 =====

    public void Hurt(int damage)
    {
        if (IsDead || IsInvincible || damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(HurtRoutine());
    }

    //扣血加血（以后可能有回血道具）
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ===== 受伤表现 =====

    private IEnumerator HurtRoutine()
    {
        invincibleTimer = invincibleTime;

        //① 先闪一下红，让玩家知道"我被打到了"
        SetColor(hurtColor);
        yield return new WaitForSeconds(flashTime);

        //② 无敌期间半透明闪烁，告诉玩家"现在打不到我"
        Color blinkColor = hurtColor;
        blinkColor.a = 0.35f;

        float remaining = invincibleTime - flashTime;

        while (remaining > 0f && !IsDead)
        {
            SetColor(blinkColor);
            yield return new WaitForSeconds(blinkInterval);

            SetColor(normalColor);
            yield return new WaitForSeconds(blinkInterval);

            remaining -= blinkInterval * 2f;
        }

        SetColor(normalColor);
    }

    private void SetColor(Color color)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }

    // ===== 死亡 =====

    private void Die()
    {
        IsDead = true;

        StopAllCoroutines();
        SetColor(normalColor);

        //锁住操作，避免死了还能走
        if (brain != null)
            brain.inputEnabled = false;

        //先简单地半透明表示"倒下了"（以后有死亡动画就换成播动画）
        if (spriteRenderer != null)
        {
            Color dead = spriteRenderer.color;
            dead.a = 0.4f;
            spriteRenderer.color = dead;
        }

        Debug.Log("玩家死亡");

        if (!reloadSceneOnDeath) return;

        //用常驻的 ScreenFader 淡出并重开当前场景（它不会被切场景销毁，所以淡入能接上）
        ScreenFader.Instance.StartCoroutine(
            ScreenFader.Instance.FadeOutAndLoad(SceneManager.GetActiveScene().name, deathFadeTime));
    }
}
