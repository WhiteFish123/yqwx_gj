// 走进范围就自动播一段对话（第一次上二楼、走近闪烁的刀、推第一只箱子时那种引导）
//
// 用法：
//   1. 物体上挂 Collider2D 并勾 Is Trigger，范围调成想触发的格子
//   2. 挂这个脚本，填 node（例：L2_Enter / L1_Knife_Hint）
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogueZone : MonoBehaviour
{
    [Header("要播放的 Yarn 节点名")]
    public string node = "L2_Enter";

    [Tooltip("只在第一次进入时播一次")]
    public bool onlyOnce = true;

    [Tooltip("留空会自动找场景里的 DialogueRunner")]
    public Yarn.Unity.DialogueRunner runner;

    private bool used;

    private void Awake()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;

        if (runner == null)
            runner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used && onlyOnce)
            return;

        // 只认玩家
        if (other.GetComponentInParent<PlayerBrain>() == null)
            return;

        if (runner == null || runner.IsDialogueRunning)
            return;

        if (string.IsNullOrEmpty(node))
        {
            Debug.LogWarning("DialogueZone 没有填节点名：" + name, this);
            return;
        }

        used = true;

        // 优先走 DialogueHotkey：顺手锁住玩家输入并接管推进/选项按键
        DialogueHotkey hotkey = FindFirstObjectByType<DialogueHotkey>();

        if (hotkey != null)
            hotkey.BeginDialogue(node);
        else
            runner.StartDialogue(node);
    }
}
