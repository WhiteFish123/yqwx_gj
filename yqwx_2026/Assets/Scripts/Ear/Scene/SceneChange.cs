using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    //开始游戏：先进开场黑幕（Intro 场景），对话播完由 IntroSequence 送进 Level_03
    public void LoadIntroScene()
    {
        SceneManager.LoadScene("Intro");
    }

    public void LoadGameScene()
    {
        SceneManager.LoadScene("Level_03");
    }
    public void LoadStartScene()
    {
        SceneManager.LoadScene("StartScene");
    }
    public void LoadSLScene()
    {
        SceneManager.LoadScene("SLScene");
    }
    public void QuitScene()
    {
        Application.Quit();
    }
}