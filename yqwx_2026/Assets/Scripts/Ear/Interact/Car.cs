using UnityEngine;

// 需要"油"才能通关的车。
//
// 判断"带没带油"时，手上和背包都算：
//   - 手上有油 → 直接卸下（卸下的物品不会回背包，等于消耗）
//   - 背包里有油 → 从背包拿走一件
//
// 加满之后：锁住玩家输入 → 交给常驻的 ScreenFader 做「淡出 → 切胜利场景 → 淡入」。
// ★ 为什么流程不写在车自己身上：切场景会销毁车，跑在车身上的协程会当场中断，
//   淡入就永远执行不到，画面会永远停在黑屏。
public class Car : MonoBehaviour, IInteractable
{
    [Header("需要的物品")]
    [Tooltip("要带着这个物品才能交互。一般拖 Item - Oil")]
    public ItemData requiredItem;

    [Header("通关")]
    [Tooltip("加满油后进入哪个场景。留空 = 不切场景，只闪一下黑屏当作反馈")]
    public string winSceneName = "Victory";

    [Tooltip("黑屏淡出/淡入各用多久")]
    public float fadeDuration = 0.6f;

    [Tooltip("一开始就是加满的状态（测试用）")]
    public bool alreadyFuelled;

    private bool fuelled;

    private void Awake()
    {
        fuelled = alreadyFuelled;
    }

    // ===== IInteractable =====

    public string GetPrompt()
    {
        if (fuelled) return "车已经加满油了";
        if (requiredItem == null) return "车打不着火";

        return HasRequiredItem() ? "给车加油" : "需要油";
    }

    public void Interact(GameObject interactor)
    {
        if (fuelled || requiredItem == null) return;

        //没带油：按 E 不做任何事（提示里已经写了"需要油"）
        if (!HasRequiredItem()) return;

        //目标场景没登记就先别消耗油，也别黑屏
        if (!IsWinSceneReady()) return;

        if (!ConsumeRequiredItem()) return;

        fuelled = true;
        FinishLevel();
    }

    // ===== 判断 / 消耗 =====

    //手上或背包里有这件物品吗
    private bool HasRequiredItem()
    {
        HeldItemSlot slot = FindFirstObjectByType<HeldItemSlot>();

        if (slot != null && slot.HeldItem != null && slot.HeldItem.data == requiredItem)
            return true;

        Inventory bag = FindFirstObjectByType<Inventory>();
        return bag != null && bag.HasItem(requiredItem);
    }

    //消耗一件：优先用掉手上的
    private bool ConsumeRequiredItem()
    {
        HeldItemSlot slot = FindFirstObjectByType<HeldItemSlot>();

        if (slot != null && slot.HeldItem != null && slot.HeldItem.data == requiredItem)
        {
            slot.Unequip();//卸下即丢，物品被消耗，不会回背包
            return true;
        }

        Inventory bag = FindFirstObjectByType<Inventory>();
        return bag != null && bag.ConsumeItem(requiredItem);
    }

    //胜利场景是否可用（必须先加进 Build Settings 的 Scene List）
    private bool IsWinSceneReady()
    {
        if (string.IsNullOrEmpty(winSceneName))
            return true;//不切场景也允许

        if (Application.CanStreamedLevelBeLoaded(winSceneName))
            return true;

        Debug.LogError("Car：场景 \"" + winSceneName + "\" 不在 Build Settings 的 Scene List 里，" +
                       "无法进入胜利界面。请先把胜利场景拖进 Scene List。", this);
        return false;
    }

    // ===== 通关表现 =====

    private void FinishLevel()
    {
        PlayerBrain brain = FindFirstObjectByType<PlayerBrain>();
        if (brain != null) brain.inputEnabled = false;

        Debug.Log("Car：通关，进入 " + (string.IsNullOrEmpty(winSceneName) ? "（未配置场景）" : winSceneName));

        //跑在常驻的 ScreenFader 上，切场景后它还活着
        ScreenFader.Instance.StartCoroutine(
            ScreenFader.Instance.FadeOutAndLoad(winSceneName, fadeDuration));
    }
}
