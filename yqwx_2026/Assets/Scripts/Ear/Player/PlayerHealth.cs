using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 玩家血量：血量可在 Inspector 里设（默认 3 滴），归零死亡并进入死亡界面。
// 受伤闪红、1 秒无敌。
//
// 谁会打我：
//   - 史莱姆 / 石像的冲刺撞到（Enemy_AttackState、Enemy_StoneChargeState 里调 Hurt(1)）
//   - 燃烧的箱子（BurningChestHazard 每帧检查）
//   - Boss（直接调 Hurt(3) —— 打满 3 点就死，不需要额外的伤害类型）
//
// 为什么伤害走 Hurt(int) 而不是只走 TakeHit：
//   TakeHit 是给"刀枪子弹"这类有 HitInfo 的东西用的（要区分伤害类型）；
//   敌人的碰撞、火焰、Boss 这种只需要点数，直接调 Hurt 更直白。
public class PlayerHealth : MonoBehaviour, IDamageable, IEnemyAttackTarget
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
    [Tooltip("死亡后进入哪个场景。留空 = 重开当前场景（当作直接重试）")]
    public string deathSceneName = "DeathScene";

    [Tooltip("死亡淡出时长")]
    public float deathFadeTime = 0.6f;

    [Header("调试")]
    [Tooltip("按这个键自己扣 1 点血，用来测闪红/无敌/死亡。默认 None = 关闭")]
    public KeyCode debugHurtKey = KeyCode.None;

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

        //调试键：不用等敌人，自己扣血就能测
        if (debugHurtKey != KeyCode.None && Input.GetKeyDown(debugHurtKey))
            Hurt(1);
    }

    // ===== IDamageable：刀枪子弹这类带类型的伤害 =====

    public void TakeHit(HitInfo info)
    {
        if (IsDead) return;

        Hurt(Mathf.Max(1, Mathf.RoundToInt(info.damage)));
    }

    // ===== 通用入口：敌人碰撞、火焰、Boss 都走这里 =====

    //ignoreInvincible：无视无敌帧。Boss 这种"只要打到就该秒"的伤害要传 true，
    //否则玩家刚好在上一击的无敌时间里，Boss 的三点伤害会被吃掉。
    public void Hurt(int damage, bool ignoreInvincible = false)
    {
        if (IsDead || damage <= 0) return;
        if (IsInvincible && !ignoreInvincible) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        StartCoroutine(HurtRoutine());
    }

    // ===== 给敌人用 =====
    //敌人程序集看不见 PlayerHealth 这个类（asmdef 边界），只能面向 IEnemyAttackTarget 接口，
    //这里实现它，敌人撞到时就调这个方法。
    public void TakeEnemyHit(int damage)
    {
        Hurt(damage);
    }

    //扣血加血（回血道具会用到）。
    //返回 true = 真的回上了；满血时返回 false，上层据此决定要不要消耗道具
    public bool Heal(int amount)
    {
        if (IsDead || amount <= 0) return false;
        if (currentHealth >= maxHealth) return false;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
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

        //死亡后进哪个场景：默认死亡界面，留空则当作重开本关
        string target = string.IsNullOrEmpty(deathSceneName)
            ? SceneManager.GetActiveScene().name
            : deathSceneName;

        //目标场景没登记进 Build Settings 时退回到"重开本关"，免得卡在黑屏
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError("PlayerHealth：场景 \"" + target + "\" 不在 Build Settings 的 Scene List 里，" +
                           "改为重开当前场景。要进死亡界面请先把它加进 Scene List。", this);

            target = SceneManager.GetActiveScene().name;
        }

        //用常驻的 ScreenFader 淡出并切场景（它不会被切场景销毁，所以淡入能接上）
        ScreenFader.Instance.StartCoroutine(
            ScreenFader.Instance.FadeOutAndLoad(target, deathFadeTime));
    }
}
