using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 需要"油"才能通关的车。
//
// 判断"带没带油"时，手上和背包都算：
//   - 手上有油 → 直接卸下（卸下的物品不会回背包，等于消耗）
//   - 背包里有油 → 从背包拿走一件
//
// 加满之后：锁住玩家输入 → 黑屏淡出 → 切到 winSceneName（默认回主菜单）→ 淡入。
// 想改成"停在原地显示通关画面"，把 winSceneName 留空即可（只在 Console 打印）。
public class Car : MonoBehaviour, IInteractable
{
    [Header("需要的物品")]
    [Tooltip("要带着这个物品才能交互。一般拖 Item - Oil")]
    public ItemData requiredItem;

    [Header("通关")]
    [Tooltip("加满油后切到哪个场景。留空 = 不切场景，只在 Console 打印「通关」")]
    public string winSceneName = "StartScene";

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

        if (!ConsumeRequiredItem()) return;

        fuelled = true;
        StartCoroutine(WinRoutine());
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

    // ===== 通关表现 =====

    private IEnumerator WinRoutine()
    {
        PlayerBrain brain = FindFirstObjectByType<PlayerBrain>();
        if (brain != null) brain.inputEnabled = false;

        yield return ScreenFader.Instance.FadeOut(fadeDuration);

        if (string.IsNullOrEmpty(winSceneName))
        {
            Debug.Log("通关！（没有配置 Win Scene Name，所以停在黑屏）");
            yield break;
        }

        SceneManager.LoadScene(winSceneName);
        yield return null;//等一帧，让新场景就绪

        //ScreenFader 是 DontDestroyOnLoad，切场景后还在，所以能接着淡入
        yield return ScreenFader.Instance.FadeIn(fadeDuration);
    }
}
