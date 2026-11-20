using UnityEditor;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 对话 UI 编辑辅助：运行时两个 Presenter 的 CanvasGroup alpha 是 0（藏起来等人说话），
/// 所以编辑模式在 Scene 视图里什么都看不到。用这个菜单临时把 alpha 拉起来调位置和样式。
/// 调完点「隐藏预览」，再保存场景，否则下次进游戏会先闪一下占位文本。
/// </summary>
public static class DialogueUiPreview
{
    private const string MenuRoot = "Tools/对话 UI 预览/";

    [MenuItem(MenuRoot + "显示预览", false, 10)]
    private static void ShowPreview()
    {
        SetAlpha(1f);
    }

    [MenuItem(MenuRoot + "隐藏预览（调完记得点这个）", false, 11)]
    private static void HidePreview()
    {
        SetAlpha(0f);
    }

    private static void SetAlpha(float alpha)
    {
        DialogueRunner runner = Object.FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
        if (runner == null)
        {
            Debug.LogWarning("[对话UI预览] 当前场景里没找到 DialogueRunner。");
            return;
        }

        int count = 0;
        foreach (CanvasGroup group in runner.GetComponentsInChildren<CanvasGroup>(true))
        {
            Undo.RecordObject(group, "对话UI预览");
            group.alpha = alpha;
            EditorUtility.SetDirty(group);
            count++;
        }

        Debug.Log($"[对话UI预览] 已把 {count} 个 CanvasGroup 的 alpha 设为 {alpha}。" +
                  (alpha > 0f ? "调完记得点「隐藏预览」再保存场景。" : "可以保存场景了。"));
    }
}
