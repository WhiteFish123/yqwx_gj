using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

//一次性工具：生成开场黑幕场景 Assets/Scenes/Intro.unity
//用「附加加载 + 不保存原场景」的方式构建，避免动到用户当前打开的 Level_03（可能有未保存改动）
public static class IntroSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Intro.unity";
    private const string FlashFolder = "Assets/Art/开车灯光特写";

    public static string Build()
    {
        var log = new StringBuilder();
        var active = SceneManager.GetActiveScene();

        //先取原场景里的引用（切换活动场景之后就不好找了）
        var dsSource = GameObject.Find("DialogueSystem");
        var esSource = GameObject.Find("EventSystem");
        if (dsSource == null)
            return "ERROR: 当前场景里没有 DialogueSystem";

        var intro = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(intro);

        //摄像机（黑底）
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.orthographicSize = 5.4f;
        camGO.AddComponent<AudioListener>();

        //EventSystem：优先复制现有场景里的（项目用 Input System，模块也跟着一起复制最稳）
        if (esSource != null)
        {
            var esCopy = Object.Instantiate(esSource);
            esCopy.name = "EventSystem";
            log.Append("eventSystem=copied ");
        }
        else
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            log.Append("eventSystem=created ");
        }

        //对话系统整体复制（内部引用会一起重建）
        var dsCopy = Object.Instantiate(dsSource);
        dsCopy.name = "DialogueSystem";
        var hotkey = dsCopy.GetComponent<DialogueHotkey>();
        if (hotkey != null)
            hotkey.brain = null; //开场场景没有玩家，清掉跨场景引用
        var runner = dsCopy.GetComponent<DialogueRunner>();
        log.Append($"dialogue={runner != null} hotkey={hotkey != null} ");

        //黑幕 + 中间的车灯循环动画
        var backdrop = new GameObject("Backdrop", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = backdrop.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        var scaler = backdrop.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(backdrop.transform, false);
        var blackRT = (RectTransform)black.transform;
        blackRT.anchorMin = Vector2.zero;
        blackRT.anchorMax = Vector2.one;
        blackRT.offsetMin = Vector2.zero;
        blackRT.offsetMax = Vector2.zero;
        var blackImage = black.GetComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;

        var flash = new GameObject("CarLight", typeof(RectTransform), typeof(Image), typeof(SpriteLoopPlayer));
        flash.transform.SetParent(backdrop.transform, false);
        var flashRT = (RectTransform)flash.transform;
        flashRT.anchorMin = new Vector2(0.5f, 0.5f);
        flashRT.anchorMax = new Vector2(0.5f, 0.5f);
        flashRT.pivot = new Vector2(0.5f, 0.5f);
        flashRT.anchoredPosition = Vector2.zero;
        flashRT.sizeDelta = new Vector2(420f, 420f);
        var flashImage = flash.GetComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.preserveAspect = true;

        var frames = new List<Sprite>();
        for (int i = 0; i < 12; i++)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{FlashFolder}/{i}.png");
            if (sp != null)
                frames.Add(sp);
        }

        var player = flash.GetComponent<SpriteLoopPlayer>();
        player.frames = frames.ToArray();
        player.fps = 12f;
        player.loop = true;
        if (frames.Count > 0)
            flashImage.sprite = frames[0];
        log.Append($"frames={frames.Count} ");

        //开场流程：开对话 → 结束 → 进 Level_03
        var seqGO = new GameObject("IntroSequence");
        var seq = seqGO.AddComponent<IntroSequence>();
        seq.runner = runner;
        seq.hotkey = hotkey;
        seq.startNode = "Start";
        seq.nextScene = "Level_03";

        EditorSceneManager.SaveScene(intro, ScenePath);
        log.Append($"saved={ScenePath} ");

        //回到用户原来的场景，把临时打开的 Intro 关掉（已保存，不需要再提示）
        if (active.IsValid())
            EditorSceneManager.SetActiveScene(active);
        EditorSceneManager.CloseScene(intro, true);

        //加入 Build Settings（放在 StartScene 后面）
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath))
        {
            int idx = list.FindIndex(s => s.path.EndsWith("StartScene.unity"));
            list.Insert(idx < 0 ? list.Count : idx + 1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            log.Append("buildSettings=inserted");
        }
        else
        {
            log.Append("buildSettings=exists");
        }

        return log.ToString();
    }
}
