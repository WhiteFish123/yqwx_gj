using UnityEngine;

//跨场景传递「下一段要播的 Yarn 节点」。
//IntroSequence 在切场景前把节点名放进来，Level_03 里的开场控制器再取走。
public static class DialogueContinuation
{
    public static string PendingNode;

    public static bool HasPending
    {
        get { return !string.IsNullOrEmpty(PendingNode); }
    }

    public static string TakePending()
    {
        string node = PendingNode;
        PendingNode = null;
        return node;
    }

    public static void Clear()
    {
        PendingNode = null;
    }
}
