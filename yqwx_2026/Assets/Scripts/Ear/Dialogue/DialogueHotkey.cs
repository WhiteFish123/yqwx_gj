using UnityEngine;
using UnityEngine.EventSystems;
using Yarn.Unity;

// 对话的键盘处理：推进 / 快进 / 选项选择，并在对话期间锁住玩家输入。
// 谁开的对话都行——开场（IntroSequence）、按 E 的交互物（DialogueInteractable）、
// 走进触发区（DialogueZone）、捡起道具（Pickup.dialogueOnPickup）都可以直接
// runner.StartDialogue(...)，这里会自动接管键盘并锁输入。
//
// 按键分工：
//   E          交互键（由 PlayerInteractor 处理）；startKey 设为 None 时这里不再管开始对话
//   W / S      选项上下选择（也认方向键）
//   空格       推进下一句 / 跳过打字机；选项出现时＝选中当前项
//   长按左Ctrl 连续快进（遇到选项会自动停下，避免误点）
public class DialogueHotkey : MonoBehaviour
{
    [Header("引用（留空自动查找）")]
    public DialogueRunner runner;
    public PlayerBrain brain;

    [Header("配置")]
    [Tooltip("直接开始 startNode 的调试按键；正式关卡请设成 None，改由交互物触发各自的节点")]
    public KeyCode startKey = KeyCode.None;

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

    [Tooltip("排查选项按键时打开：每次处理选项会打一条日志")]
    public bool debugOptions;
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

        //别人开的对话（交互物 / 触发区 / 捡东西）也要被接管：
        //一旦发现 runner 在跑，就当成自己开的来处理，锁输入、接键盘。
        if (!running && runner.IsDialogueRunning)
        {
            running = true;
            fastForwardTimer = 0f;
            SetPlayerInput(false);
        }

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

        if (startKey == KeyCode.None)
            return;

        if (!Input.GetKeyDown(startKey))
            return;

        // 附近有能按 E 的交互物时，这一下 E 是给它用的，别再开默认对话
        if (PlayerInteractor.HasInteractableInRange)
            return;

        // 暂停或背包打开时不要触发对话
        if (pauseMenu != null && pauseMenu.IsPaused)
            return;

        if (Time.timeScale <= 0f)
            return;

        // 已经有对话在跑（比如同一帧里交互物刚开了一段），不要再开
        if (runner.IsDialogueRunning)
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
        {
            // 选项面板刚弹出来、按钮还没激活的那一两帧：把确认键吃掉，
            // 否则它会落到下面的 RequestNextContent，把整段选项取消掉（＝对话直接结束）
            if (OptionsOnScreen() &&
                (Input.GetKeyDown(advanceKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                return true;

            return false;
        }

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

        if (debugOptions && confirm)
            Debug.Log("[opt] CONFIRM usable=" + visibleOptions.Count + " current=" + current, this);

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
