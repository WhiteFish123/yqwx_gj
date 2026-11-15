using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 暂停面板：按 ESC 弹出，不是独立场景，就是场景里的一个 Canvas 面板。
//
// 功能：
//   - ESC 开/关
//   - 暂停时把 Time.timeScale 设成 0（游戏逻辑全停），并锁住玩家输入
//   - 恢复时把 timeScale 还原成"暂停前的值"（所以背包开着时按 ESC 也不会把它弄乱）
//   - 音乐走 AudioManager：暂停时 PauseMusic，恢复时 ResumeMusic
//   - 三个按钮：继续 / 返回主菜单 / 进入 SLScene
//
// 搭建（要你做）：
//   1. Canvas 下放一个全屏面板（半透明黑背景 Image + 标题 + 3 个 Button）
//   2. 给这个面板挂上本脚本
//   3. 把 3 个 Button 分别拖进 Resume / Main Menu / SL Scene 三个槽
//      （不拖也行：直接在按钮的 OnClick 里选 Resume / LoadMainMenu / LoadSLScene 这三个方法）
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

    private void Awake()
    {
        if (panel == null)
            panel = gameObject;

        //按钮也可以在 Inspector 里手动连 OnClick；这里再连一次只是图方便
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenu);
        if (slSceneButton != null) slSceneButton.onClick.AddListener(LoadSLScene);

        panel.SetActive(false);
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

        panel.SetActive(true);
        SetPlayerInput(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PauseMusic();
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;

        Time.timeScale = timeScaleBeforePause;
        panel.SetActive(false);

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
        panel.SetActive(false);

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
