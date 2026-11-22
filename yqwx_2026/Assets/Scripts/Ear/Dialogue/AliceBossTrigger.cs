using UnityEngine;
using Yarn.Unity;

//BOSS 演出：玩家走进这块区域 → 爱丽丝转身面对玩家 → 播指定对话 → 对话结束后开始战斗
[RequireComponent(typeof(Collider2D))]
public class AliceBossTrigger : MonoBehaviour
{
    [Tooltip("走进来要播的对话节点")]
    public string node = "Boss_Alice_2";

    [Tooltip("要转身面对玩家的爱丽丝；留空自动找场景里的第一个")]
    public Enemy_Alice alice;

    [Tooltip("只触发一次")]
    public bool onlyOnce = true;

    [Tooltip("对话结束后让爱丽丝开始战斗")]
    public bool startFightAfter = true;

    private bool triggered;//已经触发过
    private bool waitingForDialogueEnd;//正在等这段对话播完
    private bool sawDialogueRunning;//确认对话真的跑起来过（防止开局那一帧误判）
    private DialogueRunner runner;
    private DialogueHotkey hotkey;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;

        if (alice == null)
            alice = FindFirstObjectByType<Enemy_Alice>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (onlyOnce && triggered)
            return;

        if (other.GetComponentInParent<PlayerBrain>() == null)//只认玩家
            return;

        if (alice != null)
            alice.FacePlayer();//先转身面向玩家

        if (runner == null)
            runner = FindFirstObjectByType<DialogueRunner>();

        if (runner == null)
            return;

        if (runner.IsDialogueRunning)//已经有对话在播就不打断
            return;

        if (hotkey == null)
            hotkey = FindFirstObjectByType<DialogueHotkey>();

        if (hotkey != null)
            hotkey.BeginDialogue(node);
        else
            runner.StartDialogue(node);

        triggered = true;
        waitingForDialogueEnd = true;
        sawDialogueRunning = false;
    }

    private void Update()
    {
        if (!waitingForDialogueEnd)
            return;

        if (runner == null)
        {
            waitingForDialogueEnd = false;
            return;
        }

        if (runner.IsDialogueRunning)//还在播
        {
            sawDialogueRunning = true;
            return;
        }

        if (!sawDialogueRunning)//还没开始就先别收尾
            return;

        waitingForDialogueEnd = false;

        if (startFightAfter && alice != null)
            alice.StartFight();//对话播完，开打
    }
}
