using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 传送服务：全场唯一的调度者。
//
// 流程：
//   玩家走完一格 → 检查这一格是不是某个传送点的触发区域
//   → 是：锁输入 → 黑幕淡出 0.35s → 玩家瞬移 + 相机硬贴 → 黑幕淡入 0.35s → 解输入
//
// 自动创建（RuntimeInitializeOnLoadMethod），不需要在场景里摆；
// DontDestroyOnLoad，所以以后加了跨场景也不会断。
//
// 防回环：落地之后把"落地格"记下来，只要玩家还站在这一格就不再触发，
//        等玩家走开再恢复。这样落点就算正好在目标传送阵上也不会互相弹。
public class TeleportService : MonoBehaviour
{
    [Header("节奏")]
    public float fadeOutDuration = 0.35f;
    public float fadeInDuration = 0.35f;

    private static readonly List<TeleportPoint> points = new List<TeleportPoint>();
    private static TeleportService instance;

    private PlayerGridMove motor;//玩家移动引擎（缓存，场景切换后会自动重新找）
    private PlayerBrain brain;
    private CameraFollow cameraFollow;

    private bool isTeleporting;
    private bool hasIgnoredCell;
    private Vector3Int ignoredCell;

    // ===== 注册表（静态，传送点 OnEnable 时登记） =====

    public static void Register(TeleportPoint point)
    {
        if (point == null || points.Contains(point)) return;
        points.Add(point);
    }

    public static void Unregister(TeleportPoint point)
    {
        points.Remove(point);
    }

    //进游戏时自动创建一个常驻实例，任何场景都有
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject root = new GameObject("TeleportService");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<TeleportService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);//防止场景里又摆了一个
            return;
        }

        instance = this;
    }

    private void Update()
    {
        if (isTeleporting) return;
        if (!EnsureRefs()) return;

        Vector3Int cell = motor.Cell;

        //离开落地格之后再解除忽略，避免刚落地就被弹回去
        if (hasIgnoredCell)
        {
            if (cell == ignoredCell) return;
            hasIgnoredCell = false;
        }

        for (int i = 0; i < points.Count; i++)
        {
            TeleportPoint point = points[i];

            if (point == null || point.target == null) continue;//没配目标：不触发
            if (point.target == point) continue;//目标指向自己：会原地弹死，直接忽略
            if (!point.ContainsCell(cell)) continue;

            StartCoroutine(TeleportRoutine(point));
            return;
        }
    }

    //缓存引用；被销毁（切场景）后会自动重新找
    private bool EnsureRefs()
    {
        if (motor == null) motor = FindFirstObjectByType<PlayerGridMove>();
        if (brain == null) brain = FindFirstObjectByType<PlayerBrain>();
        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();

        return motor != null;
    }

    private IEnumerator TeleportRoutine(TeleportPoint from)
    {
        isTeleporting = true;
        SetInputEnabled(false);//淡出期间不能动

        yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        //落点由目标传送点决定
        Vector3Int landing = from.target.LandingCell;

        motor.TeleportTo(landing);
        if (cameraFollow != null) cameraFollow.SnapToTarget();

        ignoredCell = landing;
        hasIgnoredCell = true;

        yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        SetInputEnabled(true);
        isTeleporting = false;
    }

    private void SetInputEnabled(bool value)
    {
        if (brain != null) brain.inputEnabled = value;
    }
}
