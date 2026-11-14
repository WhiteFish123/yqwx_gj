using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    public void LoadGameScene()
    {
        SceneManager.LoadScene("GameScene1");
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