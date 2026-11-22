using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

//开场流程控制器（跨场景常驻）：
//1. 在 Intro 场景播 startNode；
//2. 播完后淡入黑场，切到 nextScene，并把 continueNode 交给目标场景；
//3. 目标场景加载后自动接着播 continueNode，同时从黑场慢慢显现；
//4. 这段对话播完后把玩家（隐藏的）恢复显示。
public class IntroSequence : MonoBehaviour
{
    [Header("对话")]
    [Tooltip("Intro 场景里开始播放的节点")]
    public string startNode = "L1_Drive";
    [Tooltip("切场景后在目标场景里接着播放的节点；留空 = 到新场景直接显现、不再播对话")]
    public string continueNode = "";
    public DialogueRunner runner;
    public DialogueHotkey hotkey;

    [Header("切换")]
    [Tooltip("对话结束后要加载的场景")]
    public string nextScene = "Level_03";
    [Tooltip("淡到全黑用多久")]
    public float fadeToBlackDuration = 0.5f;
    [Tooltip("进入新场景后从黑场显现用多久")]
    public float fadeInDuration = 3f;

    [Header("音乐")]
    [Tooltip("切场景时把当前音乐掐断")]
    public bool stopMusicOnCut = true;
    [Tooltip("画面显现完之后重播哪首音乐；留空 = 重播刚才被掐断的那首")]
    public string restartMusicId = "";

    private enum Phase
    {
        IntroDialogue,   // 开场场景里播 startNode
        FadingToBlack,   // 淡入黑场
        WaitingForScene, // 等目标场景加载完成
        LevelDialogue,   // 在新场景里接着播 continueNode
        Done
    }

    private static IntroSequence current;

    private Phase phase = Phase.IntroDialogue;
    private CanvasGroup overlay;
    private bool sawDialogueRunning;
    private float timer;
    private SpriteRenderer[] playerRenderers;
    private string pendingMusicId;
    private bool musicRestarted;
    private bool noDialogueContinuation;

    private void Awake()
    {
        //同一时间只留一个
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }
        current = this;
        DontDestroyOnLoad(gameObject);

        overlay = CreateOverlay();
        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    private void Start()
    {
        if (runner == null)
            runner = FindFirstObjectByType<DialogueRunner>();
        if (hotkey == null)
            hotkey = FindFirstObjectByType<DialogueHotkey>();

        if (runner == null)
        {
            Debug.LogWarning("[开场] 场景里找不到 DialogueRunner，跳不过去。", this);
            return;
        }

        if (hotkey != null)
            hotkey.BeginDialogue(startNode);
        else
            runner.StartDialogue(startNode);

        phase = Phase.IntroDialogue;
        sawDialogueRunning = false;
    }

    private void Update()
    {
        switch (phase)
        {
            case Phase.IntroDialogue:
                TickIntroDialogue();
                break;

            case Phase.FadingToBlack:
                TickFadeToBlack();
                break;

            case Phase.WaitingForScene:
                TickWaitingForScene();
                break;

            case Phase.LevelDialogue:
                TickLevelDialogue();
                break;
        }
    }

    //---- 各阶段 ----

    private void TickIntroDialogue()
    {
        if (runner != null && runner.IsDialogueRunning)
        {
            sawDialogueRunning = true;
            return;
        }

        if (!sawDialogueRunning)
            return;

        //开场那段播完了：音乐掐断，淡到全黑，再切场景
        StopMusicForCut();
        phase = Phase.FadingToBlack;
        timer = 0f;
    }

    private void TickFadeToBlack()
    {
        timer += Time.unscaledDeltaTime;
        SetOverlayAlpha(fadeToBlackDuration <= 0.01f ? 1f : Mathf.Clamp01(timer / fadeToBlackDuration));

        if (timer < fadeToBlackDuration)
            return;

        SetOverlayAlpha(1f);
        DialogueContinuation.PendingNode = continueNode;
        phase = Phase.WaitingForScene;

        if (!string.IsNullOrEmpty(nextScene))
        {
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.LogWarning("[开场] nextScene 没填，留在当前场景继续。", this);
            BeginLevelDialogue();
        }
    }

    private void TickWaitingForScene()
    {
        //LoadScene 是同步的，加载完成后这里再接管（此时新场景的 Awake/Start 都跑过了）
        if (SceneManager.GetActiveScene().name != nextScene)
            return;

        BeginLevelDialogue();
    }

    private void BeginLevelDialogue()
    {
        runner = FindFirstObjectByType<DialogueRunner>();
        hotkey = FindFirstObjectByType<DialogueHotkey>();

        string node = DialogueContinuation.TakePending();
        if (string.IsNullOrEmpty(node))
            node = continueNode;

        timer = 0f;
        sawDialogueRunning = false;

        if (runner != null && !string.IsNullOrEmpty(node))
        {
            //开场对话期间先藏起玩家，等这段播完再让他出现
            HidePlayer();

            if (hotkey != null)
                hotkey.BeginDialogue(node);
            else
                runner.StartDialogue(node);

            phase = Phase.LevelDialogue;
            return;
        }

        //没有后续对话：不藏玩家，直接从黑场显现，显现完放音乐收工
        noDialogueContinuation = true;
        phase = Phase.LevelDialogue;
    }

    private void TickLevelDialogue()
    {
        //一边接着播对话，一边从黑场慢慢显现
        timer += Time.unscaledDeltaTime;
        SetOverlayAlpha(fadeInDuration <= 0.01f ? 0f : Mathf.Clamp01(1f - timer / fadeInDuration));

        //画面完全显现之后再重新放音乐
        if (timer >= fadeInDuration)
            RestartMusicIfNeeded();

        if (noDialogueContinuation)
        {
            if (timer < fadeInDuration)
                return;

            //显现完了：玩家本来就是可见的，控制器退场
            SetOverlayAlpha(0f);
            phase = Phase.Done;
            Destroy(gameObject, 0.2f);
            return;
        }

        if (runner != null && runner.IsDialogueRunning)
        {
            sawDialogueRunning = true;
            return;
        }

        if (!sawDialogueRunning)
            return;

        //开场对话彻底结束：玩家现身，控制器退场
        RestartMusicIfNeeded();
        SetOverlayAlpha(0f);
        ShowPlayer();
        phase = Phase.Done;
        Destroy(gameObject, 0.2f);
    }

    //---- 音乐 ----

    private void StopMusicForCut()
    {
        if (!stopMusicOnCut || AudioManager.Instance == null)
            return;

        pendingMusicId = string.IsNullOrEmpty(restartMusicId) ? AudioManager.Instance.CurrentMusicId : restartMusicId;
        AudioManager.Instance.StopMusic();
        musicRestarted = false;
    }

    private void RestartMusicIfNeeded()
    {
        if (musicRestarted || string.IsNullOrEmpty(pendingMusicId) || AudioManager.Instance == null)
            return;

        musicRestarted = true;
        AudioManager.Instance.PlayMusic(pendingMusicId);
    }

    //---- 玩家显隐 ----

    private void HidePlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return;

        playerRenderers = player.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in playerRenderers)
            renderer.enabled = false;
    }

    private void ShowPlayer()
    {
        if (playerRenderers == null)
            return;

        foreach (SpriteRenderer renderer in playerRenderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }
        playerRenderers = null;
    }

    //---- 黑场遮罩 ----

    private CanvasGroup CreateOverlay()
    {
        GameObject canvasObject = new GameObject("FadeOverlay");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject imageObject = new GameObject("Black");
        imageObject.transform.SetParent(canvasObject.transform, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return group;
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlay != null)
            overlay.alpha = Mathf.Clamp01(alpha);
    }
}
