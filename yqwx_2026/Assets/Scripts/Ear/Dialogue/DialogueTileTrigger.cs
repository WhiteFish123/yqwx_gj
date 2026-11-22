// 走到指定的格子就播一段对话（不需要 Collider，也不用拆 Tilemap）
//
// 为什么需要它：
//   Tilemap 是一整张，`Floor_L1`、`Walls_L2` 之类都是一整张图，
//   没法像普通物体那样"给某一格挂个脚本"。所以这里反过来做：
//   脚本自己盯着玩家所在的格子，格子落在名单里就触发。
//
// 用法：
//   1. 在场景里新建一个空物体（名字随意，比如 Zone_L2_Enter），位置放到想触发的那格里
//   2. 挂上这个脚本
//   3. Grid 留空会自动找场景里的 Grid；如果这个房间有独立的 L1_Grid / L2_Grid，就在 Grid 里指定它
//   4. Node 填 Yarn 节点名（例：L2_Enter）
//   5. 右键组件 →「用当前位置作为触发格」，把物体现在站的格子填进 Cells；
//      想覆盖多格就点 Add 手动加，或者在 Inspector 里把 Cells 的 Size 调大逐个填
//   6. （可选）打开 Use Rect 并填 Min/Max 格子，直接用一个矩形范围触发
//
// 注意：格子坐标是"Grid 局部坐标"，和你在 Tilemap 里看到的格子坐标一致。
using UnityEngine;

public class DialogueTileTrigger : MonoBehaviour
{
    [Header("要播放的 Yarn 节点名")]
    public string node = "L2_Enter";

    [Tooltip("只播一次（离开再回来不会重复播）")]
    public bool onlyOnce = true;

    [Tooltip("留空会自动找场景里的 Grid；有多套 Grid 时请手动指定这一个触发区所属的 Grid")]
    public Grid grid;

    [Header("触发格（Grid 局部坐标）")]
    [Tooltip("这些格子里的任意一格都会触发")]
    public Vector3Int[] cells;

    [Header("或者：用一个矩形范围（含边界）")]
    public bool useRect;
    public Vector3Int rectMin;
    public Vector3Int rectMax;

    [Tooltip("留空会自动找场景里的 DialogueRunner")]
    public Yarn.Unity.DialogueRunner runner;

    private bool used;
    private bool wasInside;
    private Transform player;

    private void Awake()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (runner == null)
            runner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();
    }

    private void Update()
    {
        if (used && onlyOnce)
            return;

        if (grid == null || string.IsNullOrEmpty(node))
            return;

        if (runner == null)
        {
            runner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();

            if (runner == null)
                return;
        }

        if (player == null)
        {
            PlayerGridMove move = FindFirstObjectByType<PlayerGridMove>();

            if (move == null)
                return;

            player = move.transform;
        }

        Vector3Int cell = grid.WorldToCell(player.position);
        bool inside = IsTriggerCell(cell);

        //只在"踏进来"的那一下触发，站着不动不会反复播
        bool justEntered = inside && !wasInside;
        wasInside = inside;

        if (!justEntered)
            return;

        if (runner.IsDialogueRunning)
            return;

        used = true;

        //优先走 DialogueHotkey：顺手锁住玩家输入并接管推进/选项按键
        DialogueHotkey hotkey = FindFirstObjectByType<DialogueHotkey>();

        if (hotkey != null)
            hotkey.BeginDialogue(node);
        else
            runner.StartDialogue(node);
    }

    //这个格子算不算触发区
    private bool IsTriggerCell(Vector3Int cell)
    {
        if (cells != null)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == cell)
                    return true;
            }
        }

        if (useRect)
        {
            return cell.x >= Mathf.Min(rectMin.x, rectMax.x)
                && cell.x <= Mathf.Max(rectMin.x, rectMax.x)
                && cell.y >= Mathf.Min(rectMin.y, rectMax.y)
                && cell.y <= Mathf.Max(rectMin.y, rectMax.y);
        }

        return false;
    }

    //右键组件菜单：把物体当前所在的格子填进 Cells
    [ContextMenu("用当前位置作为触发格")]
    private void AddCurrentCell()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid == null)
        {
            Debug.LogWarning("没有找到 Grid，先在 Inspector 里指定一个", this);
            return;
        }

        Vector3Int cell = grid.WorldToCell(transform.position);

        foreach (Vector3Int c in cells)
        {
            if (c == cell)
            {
                Debug.Log("这个格子已经在列表里了：" + cell, this);
                return;
            }
        }

        System.Collections.Generic.List<Vector3Int> list = new System.Collections.Generic.List<Vector3Int>(cells);
        list.Add(cell);
        cells = list.ToArray();

        Debug.Log("已加入触发格：" + cell, this);
    }

    //在 Scene 视图里把这些格子画出来，方便核对
    private void OnDrawGizmosSelected()
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid == null)
            return;

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.45f);

        if (cells != null)
        {
            foreach (Vector3Int cell in cells)
                DrawCell(cell);
        }

        if (useRect)
        {
            int x0 = Mathf.Min(rectMin.x, rectMax.x);
            int x1 = Mathf.Max(rectMin.x, rectMax.x);
            int y0 = Mathf.Min(rectMin.y, rectMax.y);
            int y1 = Mathf.Max(rectMin.y, rectMax.y);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                    DrawCell(new Vector3Int(x, y, 0));
            }
        }
    }

    private void DrawCell(Vector3Int cell)
    {
        Vector3 center = grid.GetCellCenterWorld(cell);
        Vector3 size = grid.cellSize;

        Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, 0.1f));
    }
}
