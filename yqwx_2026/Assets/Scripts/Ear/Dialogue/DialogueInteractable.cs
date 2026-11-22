// 挂在会说话的交互物上（蘑菇、遗像、画像、柱子、油桶、箱子堆……）
// 玩家走近后按 E，就播放指定的 Yarn 节点。
//
// 用法：
//   1. 物体上要有 Collider2D（BoxCollider2D 就行，不需要 Trigger）
//   2. 挂这个脚本，把 node 填成 Yarn 里的节点名（例：L1_Mushroom）
//   3. prompt 是底部提示文字（例：查看）
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogueInteractable : MonoBehaviour, IInteractable
{
    [Header("要播放的 Yarn 节点名")]
    public string node = "L1_Mushroom";

    [Tooltip("底部提示文字，例：查看")]
    public string prompt = "查看";

    [Tooltip("只播一次；播完后这个物体就不能再交互了")]
    public bool onlyOnce = true;

    [Tooltip("播完后把整个物体隐藏（比拾取走掉的东西）")]
    public bool hideAfterUse = false;

    [Tooltip("留空会自动找场景里的 DialogueRunner")]
    public Yarn.Unity.DialogueRunner runner;

    private bool used;
    private Collider2D body;

    private void Awake()
    {
        body = GetComponent<Collider2D>();

        if (runner == null)
            runner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();
    }

    public string GetPrompt()
    {
        return string.IsNullOrEmpty(prompt) ? "查看" : prompt;
    }

    public void Interact(GameObject interactor)
    {
        if (used && onlyOnce)
            return;

        if (runner == null)
        {
            Debug.LogWarning("DialogueInteractable 找不到 DialogueRunner：" + name, this);
            return;
        }

        if (runner.IsDialogueRunning)
            return;

        if (string.IsNullOrEmpty(node))
        {
            Debug.LogWarning("DialogueInteractable 没有填节点名：" + name, this);
            return;
        }

        used = true;

        // 优先走 DialogueHotkey：它会顺手锁住玩家输入，并接管推进/选项按键
        DialogueHotkey hotkey = FindFirstObjectByType<DialogueHotkey>();

        if (hotkey != null)
            hotkey.BeginDialogue(node);
        else
            runner.StartDialogue(node);

        // 只播一次的：播完就别再让玩家看到提示了
        if (onlyOnce && body != null)
            body.enabled = false;

        if (hideAfterUse)
            gameObject.SetActive(false);
    }
}
