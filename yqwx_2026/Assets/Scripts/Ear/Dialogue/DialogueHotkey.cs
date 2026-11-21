using UnityEngine;
using UnityEngine.EventSystems;
using Yarn.Unity;

// 最小可玩验证用：游戏中按 E 直接开始一段 Yarn 对话。
// 对话期间锁住玩家输入（含手持物按空格点火），对话结束后自动解锁。
//
// 按键分工：
//   E          开始对话
//   W / S      选项上下选择（也认方向键）
//   空格       推进下一句 / 跳过打字机；选项出现时＝选中当前项
//   长按左Ctrl 连续快进（遇到选项会自动停下，避免误点）
// 正式版应该改成「走到 NPC 面前按 E 触发对应节点」，这里只是先用快捷键把链路跑通。
public class DialogueHotkey : MonoBehaviour
{
    [Header("引用（留空自动查找）")]
    public DialogueRunner runner;
    public PlayerBrain brain;

    [Header("配置")]
    [Tooltip("开始对话的按键")]
    public KeyCode startKey = KeyCode.E;

    [Tooltip("对话中推进/确认的按键")]
    public KeyCode advanceKey = KeyCode.Space;

    [Tooltip("要播放的 Yarn 节点名")]
    public string startNode = "Start";

    [Tooltip("长按这个键可以连续快进对话")]
    public KeyCode fastForwardKey = KeyCode.LeftControl;

    [Tooltip("快进时每隔多少秒推进一句")]
    public float fastForwardInterval = 0.07f;

    private bool running;
    private float fastForwardTimer;
    private PauseMenu pauseMenu;
    private OptionsPresenter optionsPresenter;
    private readonly System.Collections.Generic.List<OptionItem> visibleOptions = new();

    private void Awake()
    {
        if (runner == null)
            runner = FindFirstObjectByType<DialogueRunner>();

        if (brain == null)
            brain = FindFirstObjectByType<PlayerBrain>();

        pauseMenu = FindFirstObjectByType<PauseMenu>();

        if (runner != null)
            optionsPresenter = runner.GetComponentInChildren<OptionsPresenter>(true);
    }

    // 给外部（开场黑幕、剧情触发等）用：由别人决定什么时候开始播哪一段，
    // 但推进 / 快进 / 选项的键盘处理仍然交给这里，不用重复实现一套。
    public void BeginDialogue(string node)
    {
        if (runner == null)
            return;

        if (runner.IsDialogueRunning)
            return;

        running = true;
        fastForwardTimer = 0f;
        SetPlayerInput(false);
        runner.StartDialogue(string.IsNullOrEmpty(node) ? startNode : node);
    }

    private void Update()
    {
        if (runner == null)
            return;

        if (running)
        {
            // 对话刚结束：把输入还回去
            if (!runner.IsDialogueRunning)
            {
                running = false;
                fastForwardTimer = 0f;
                SetPlayerInput(true);
                return;
            }

            // 出现选项时：W/S（或上下方向键）切换，空格/回车 确认
            if (TryHandleOptions())
                return;

            // 长按快进：每隔一点时间推进一句。
            // 选项面板正在显示时不快进——否则 RequestNextContent 会把选项取消掉，等于替玩家乱选。
            if (Input.GetKey(fastForwardKey) && !OptionsOnScreen())
            {
                fastForwardTimer -= Time.unscaledDeltaTime;
                if (fastForwardTimer <= 0f)
                {
                    fastForwardTimer = Mathf.Max(0.01f, fastForwardInterval);
                    runner.RequestNextContent();
                }

                return;
            }

            fastForwardTimer = 0f;

            // 对话中按空格：推进下一句 / 跳过打字机
            if (Input.GetKeyDown(advanceKey))
                runner.RequestNextContent();

            return;
        }

        if (!Input.GetKeyDown(startKey))
            return;

        // 暂停或背包打开时不要触发对话
        if (pauseMenu != null && pauseMenu.IsPaused)
            return;

        if (Time.timeScale <= 0f)
            return;

        running = true;
        SetPlayerInput(false);
        runner.StartDialogue(startNode);
    }

    // 选项面板是否正显示在屏幕上（用来阻止快进把选项跳掉）
    private bool OptionsOnScreen()
    {
        if (optionsPresenter == null)
            return false;

        var group = optionsPresenter.GetComponent<CanvasGroup>();
        if (group != null && group.alpha > 0.01f)
            return true;

        foreach (var item in optionsPresenter.GetComponentsInChildren<OptionItem>(false))
        {
            if (item.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    // 选项出现时接管键盘：W/S 或上下键切换，空格（也认回车）确认。
    // 返回 true 表示这一帧的按键已经被选项吃掉了，不要再推进对话。
    private bool TryHandleOptions()
    {
        if (optionsPresenter == null)
            return false;

        visibleOptions.Clear();
        foreach (var item in optionsPresenter.GetComponentsInChildren<OptionItem>(false))
        {
            if (item.gameObject.activeInHierarchy && item.IsInteractable())
                visibleOptions.Add(item);
        }

        if (visibleOptions.Count == 0)
            return false;

        var es = EventSystem.current;
        if (es == null)
            return false;

        int current = IndexOfSelected(es);
        bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);

        if (up || down)
        {
            if (current < 0)
                current = 0;
            else
                current = (current + (down ? 1 : -1) + visibleOptions.Count) % visibleOptions.Count;

            es.SetSelectedGameObject(visibleOptions[current].gameObject);
            return true;
        }

        bool confirm = Input.GetKeyDown(advanceKey)
                       || Input.GetKeyDown(KeyCode.Return)
                       || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (!confirm)
            return false;

        var target = visibleOptions[current < 0 ? 0 : current].gameObject;
        es.SetSelectedGameObject(target);
        ExecuteEvents.Execute(target, new BaseEventData(es), ExecuteEvents.submitHandler);
        return true;
    }

    private int IndexOfSelected(EventSystem es)
    {
        var selected = es.currentSelectedGameObject;
        if (selected == null)
            return -1;

        for (int i = 0; i < visibleOptions.Count; i++)
        {
            if (visibleOptions[i].gameObject == selected)
                return i;
        }

        return -1;
    }

    private void SetPlayerInput(bool enabled)
    {
        if (brain != null)
            brain.inputEnabled = enabled;
    }
}
