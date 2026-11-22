//这个是掌控交互系统的脚本，挂在玩家身上
//
// 两种交互分开按键：
//   地上散落的物品（Pickup）：要站在它面前（朝它那一格），按 F 捡起
//   其他可交互物（宝箱、门等）：按 E
//
// 提示方式：
//   地上物品不再显示底部文字，而是在物品旁边显示一个闪烁的 F 键图标
//   （kb_dark_all_81 亮 / kb_dark_all_80 暗 交替闪），图标出现在玩家对面的那一侧
//   其他可交互物仍然用底部文字提示 [E] xxx
using UnityEngine;
using TMPro;

public class PlayerInteractor : MonoBehaviour
{
    [Header("交互设置")]
    [Tooltip("宝箱/门这类普通交互物用的键")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("捡地上物品用的键")]
    public KeyCode pickupKey = KeyCode.F;

    [Tooltip("交互距离（世界单位，一格 = 1）")]
    public float interactRange = 1.5f;

    [Tooltip("捡物品时要不要先转向它（关掉则只要靠近就能捡）")]
    public bool requireFacingForPickup = true;

    [Tooltip("判定“面朝”的力度：0.7 ≈ 左右 45 度以内算正面")]
    [Range(0f, 1f)]
    public float facingDot = 0.7f;

    public LayerMask interactableLayers = ~0;

    [Header("F 键图标")]
    [Tooltip("亮的那一帧（kb_dark_all_81）")]
    public Sprite keyHintLight;

    [Tooltip("暗的那一帧（kb_dark_all_80）")]
    public Sprite keyHintDark;

    [Tooltip("图标缩放；精灵本身 16 像素、100 PPU = 0.16 世界单位，5 倍约 0.8 格")]
    public float keyHintScale = 5f;

    [Tooltip("图标离物品中心的水平距离（世界单位）")]
    public float keyHintOffset = 0.55f;

    [Tooltip("亮暗切换间隔（秒）")]
    public float keyHintBlinkInterval = 0.18f;

    [Tooltip("图标排序号；要盖在角色、车子之上")]
    public int keyHintSortingOrder = 20000;

    [Header("其他交互的底部文字提示")]
    public TextMeshProUGUI promptText;

    private IInteractable nearest;// 记录最近的可交互对象
    private PlayerGridMove motor;

    //场景里现在有没有"按 E 能交互的东西"。
    //DialogueHotkey 用它判断 E 是不是已经被交互吃掉了，避免同一帧又开一段默认对话。
    public static bool HasInteractableInRange { get; private set; }

    private SpriteRenderer keyHint;// 物品旁边的闪烁 F
    private float blinkTimer;
    private bool blinkLight = true;
    private Yarn.Unity.DialogueRunner dialogueRunner;

    private void Awake()
    {
        motor = GetComponent<PlayerGridMove>();
        EnsureKeyHint();
    }

    private void EnsureKeyHint()
    {
        if (keyHint != null)
            return;

        GameObject go = new GameObject("KeyHintF");
        keyHint = go.AddComponent<SpriteRenderer>();
        keyHint.sortingOrder = keyHintSortingOrder;
        keyHint.enabled = false;
        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, keyHintScale);
    }

    void Update()
    {
        nearest = FindNearest();

        // 给 DialogueHotkey 用：附近有能按 E 交互的东西时，E 归交互
        HasInteractableInRange = nearest != null;

        // 地上物品：用物品旁边的 F 图标；其他：底部文字
        Pickup pickup = nearest as Pickup;
        bool showKeyHint = pickup != null && pickup.CanPickUp;

        UpdateKeyHint(showKeyHint, pickup);

        // 对话进行的时候，底部提示（[E] 查看）要收起来，别压在对话框上
        bool dialogueActive = IsDialogueRunning();

        if (promptText != null)
        {
            // 能被 F 捡的时候，底部文字让位给 F 图标
            if (nearest != null && !showKeyHint && !dialogueActive)
            {
                bool isPickup = nearest is Pickup;
                promptText.text = "[" + (isPickup ? "F" : "E") + "] " + nearest.GetPrompt();
                promptText.gameObject.SetActive(true);
            }
            else
            {
                promptText.gameObject.SetActive(false);
            }
        }

        if (nearest == null)
            return;

        //地上物品按 F，其余按 E
        KeyCode key = nearest is Pickup ? pickupKey : interactKey;

        if (Input.GetKeyDown(key))
            nearest.Interact(gameObject);// 调用交互方法
    }

    // 物品旁边闪烁的 F：位置在玩家对面那一侧
    private void UpdateKeyHint(bool show, Pickup pickup)
    {
        EnsureKeyHint();

        if (!show)
        {
            if (keyHint.enabled)
                keyHint.enabled = false;

            return;
        }

        if (!keyHint.enabled)
        {
            keyHint.enabled = true;
            blinkTimer = 0f;
            blinkLight = true;
        }

        // 图标出现在物品的右边；只有玩家站在物品右边时才改到左边
        // （正上方/正下方时 x 相等，按“玩家在左边”处理 → 出在右边）
        Vector3 itemPos = pickup.transform.position;
        bool hintOnRight = transform.position.x <= itemPos.x + 0.01f;
        float x = hintOnRight ? keyHintOffset : -keyHintOffset;

        keyHint.transform.position = new Vector3(itemPos.x + x, itemPos.y, itemPos.z);
        keyHint.transform.localScale = Vector3.one * Mathf.Max(0.01f, keyHintScale);

        // 一亮一暗交替
        blinkTimer += Time.deltaTime;

        if (blinkTimer >= Mathf.Max(0.02f, keyHintBlinkInterval))
        {
            blinkTimer = 0f;
            blinkLight = !blinkLight;
        }

        Sprite wanted = blinkLight ? keyHintLight : keyHintDark;
        if (wanted != null && keyHint.sprite != wanted)
            keyHint.sprite = wanted;
    }

    // 对话在播的时候不显示底部文字提示
    private bool IsDialogueRunning()
    {
        if (dialogueRunner == null)
            dialogueRunner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();

        return dialogueRunner != null && dialogueRunner.IsDialogueRunning;
    }

    // 查找最近的可交互对象（地上物品必须站在它前面才算）
    IInteractable FindNearest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange, interactableLayers);// 获取范围内的所有碰撞体
        IInteractable best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            IInteractable target = hit.GetComponent<IInteractable>();
            if (target == null) continue;

            //地上的物品：必须面朝着它
            if (requireFacingForPickup && target is Pickup && !IsFacing(hit.transform))
                continue;

            float distance = Vector2.Distance(transform.position, hit.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = target;
            }
        }

        return best;
    }

    //目标是否落在玩家面朝的那一侧
    private bool IsFacing(Transform target)
    {
        if (motor == null || target == null)
            return true;

        Vector2Int facing = motor.Facing;

        if (facing == Vector2Int.zero)
            return true;

        Vector2 delta = (Vector2)(target.position - transform.position);

        if (delta.sqrMagnitude < 0.0001f)
            return true;

        float dot = Vector2.Dot(delta.normalized, new Vector2(facing.x, facing.y));

        return dot >= facingDot;
    }
}
