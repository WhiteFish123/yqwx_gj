using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 暂停面板：按 ESC 弹出。不是独立场景，就是场景里的一个 Canvas 面板。
//
// 功能：
//   - ESC 开/关
//   - 暂停时 Time.timeScale = 0（游戏逻辑全停），并锁住玩家输入
//   - 恢复时把 timeScale 还原成"暂停前的值"（所以背包开着时按 ESC 也不会弄乱）
//   - 音乐走 AudioManager：暂停 PauseMusic，恢复 ResumeMusic
//   - 三个按钮：继续 / 返回主菜单 / 进入 SLScene
//
// ★ 显隐方式（很重要）：
//   如果脚本就挂在面板物体上，绝对不能用 SetActive(false) —— 那会把脚本自己也关掉，
//   Update 不再执行，ESC 就再也打不开了。这种情况自动改用 CanvasGroup 调透明度。
//   脚本挂在别的常驻物体上（Panel 指向子物体）时，才会用 SetActive。
//
// 搭建建议：
//   1. Canvas 下建空物体 PauseMenuManager（常驻激活）挂本脚本
//   2. 它下面放 PausePanel（背景 + 标题 + 3 个按钮），拖进 Panel 槽
//   3. 3 个按钮分别拖进 Resume / Main Menu / SL Scene
//   （也可以把脚本直接挂 PausePanel 上，但面板物体必须保持激活）
public class PauseMenu : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("暂停面板的根物体。留空就用脚本所在的物体")]
    public GameObject panel;

    [Tooltip("继续按钮（可选）")]
    public Button resumeButton;

    [Tooltip("返回主菜单按钮（可选）")]
    public Button mainMenuButton;

    [Tooltip("进入 SLScene 按钮（可选）")]
    public Button slSceneButton;

    [Header("按键")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("切场景")]
    [Tooltip("返回主菜单的场景名")]
    public string mainMenuScene = "StartScene";

    [Tooltip("SLScene 的场景名")]
    public string slSceneName = "SLScene";

    [Tooltip("淡出用多久")]
    public float fadeTime = 0.4f;

    public bool IsPaused { get; private set; }

    private float timeScaleBeforePause = 1f;
    private PlayerBrain brain;
    private CanvasGroup panelGroup;
    private static PauseMenu active;//场景里只允许一个暂停面板（否则两份脚本都监听 ESC，按一下开又关）

    private void Awake()
    {
        //防止场景里不小心放了两个暂停面板：后来的自动销毁
        if (active != null && active != this)
        {
            Debug.LogWarning("PauseMenu：场景里已经有一个暂停面板了，多余的会被移除", this);
            Destroy(gameObject);
            return;
        }

        active = this;

        if (panel == null)
            panel = gameObject;

        //做成 prefab 拖到任意场景都能用：不在 Canvas 下就自己建一个
        EnsureCanvas();

        //按钮也可以在 Inspector 里手动连 OnClick；这里再连一次只是图方便
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenu);
        if (slSceneButton != null) slSceneButton.onClick.AddListener(LoadSLScene);

        SetVisible(false);

        //没有 EventSystem 的话按钮点不了（ESC 仍然可用），提前提醒
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            Debug.LogWarning("PauseMenu：场景里没有 EventSystem，暂停面板的按钮点不了（ESC 仍然可用）。" +
                             "给场景加一个 UI → Event System 即可。", this);
    }

    //如果这个面板不在任何 Canvas 下，就自己建一个 Canvas 当父物体。
    //这样把整个面板做成 prefab 后，丢到任何场景（哪怕场景里没有 Canvas）都能正常显示。
    private void EnsureCanvas()
    {
        if (GetComponentInParent<Canvas>() != null)
            return;

        GameObject canvasGo = new GameObject("PauseMenuCanvas");

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;//盖在普通 UI 上面

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();//没它按钮收不到点击

        transform.SetParent(canvasGo.transform, false);
    }

    //显隐面板。
    //★ 脚本挂在面板自己身上时不能用 SetActive(false)：那会把脚本（含 Update）一起关掉，
    //  ESC 就再也打不开了。这种情况改用 CanvasGroup 调透明度，物体保持激活。
    private void SetVisible(bool visible)
    {
        if (panel == null)
            return;

        if (panel == gameObject)
        {
            if (panelGroup == null)
            {
                panelGroup = panel.GetComponent<CanvasGroup>();
                if (panelGroup == null)
                    panelGroup = panel.AddComponent<CanvasGroup>();
            }

            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.blocksRaycasts = visible;
            panelGroup.interactable = visible;
            return;
        }

        panel.SetActive(visible);
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
            Toggle();
    }

    // ===== 开关 =====

    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;

        IsPaused = true;

        //记下暂停前的值：背包打开时它本来就是 0，恢复时才会正确还原
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        SetVisible(true);
        SetPlayerInput(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PauseMusic();
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;

        Time.timeScale = timeScaleBeforePause;
        SetVisible(false);

        //只有游戏真的在跑时才把操作还回去（背包还开着时保持锁住）
        SetPlayerInput(!Mathf.Approximately(Time.timeScale, 0f));

        if (AudioManager.Instance != null)
            AudioManager.Instance.ResumeMusic();
    }

    // ===== 按钮 =====

    public void LoadMainMenu()
    {
        LoadSceneWithFade(mainMenuScene);
    }

    public void LoadSLScene()
    {
        LoadSceneWithFade(slSceneName);
    }

    // ===== 内部 =====

    private void LoadSceneWithFade(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("PauseMenu：场景 \"" + sceneName + "\" 不在 Build Settings 的 Scene List 里，跳转失败", this);
            return;
        }

        //先还原时间，避免切过去还是暂停状态
        IsPaused = false;
        Time.timeScale = 1f;
        SetVisible(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.ResumeMusic();

        //用常驻的 ScreenFader 淡出再切（它不会被切场景销毁）
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.StartCoroutine(
                ScreenFader.Instance.FadeOutAndLoad(sceneName, fadeTime));
            return;
        }

        SceneManager.LoadScene(sceneName);//没有黑幕就直切
    }

    private void SetPlayerInput(bool enabled)
    {
        if (brain == null)
            brain = FindFirstObjectByType<PlayerBrain>();

        if (brain != null)
            brain.inputEnabled = enabled;
    }
}
