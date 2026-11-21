using UnityEditor;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 对话 UI 编辑辅助：运行时两个 Presenter 的 CanvasGroup alpha 是 0（藏起来等人说话），
/// 所以编辑模式在 Scene 视图里什么都看不到。用这个菜单临时把 alpha 拉起来调位置和样式。
/// 调完点「隐藏预览」，再保存场景，否则下次进游戏会先闪一下占位文本。
/// 立绘已经移到 Canvas 下（不跟随对话框淡入淡出），不在任何 CanvasGroup 里，
/// 所以这里单独控制它的显隐，否则「隐藏预览」会留下一个立绘在场景里。
/// </summary>
public static class DialogueUiPreview
{
    private const string MenuRoot = "Tools/对话 UI 预览/";
    private const string PortraitName = "Portrait";

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

        // 立绘不在 CanvasGroup 里，单独开关物体
        Transform portrait = FindDeep(runner.transform, PortraitName);
        if (portrait != null)
        {
            Undo.RecordObject(portrait.gameObject, "对话UI预览");
            portrait.gameObject.SetActive(alpha > 0f);
            EditorUtility.SetDirty(portrait.gameObject);
            count++;
        }

        Debug.Log($"[对话UI预览] 已把 {count} 个对话 UI 元素设为 {(alpha > 0f ? "显示" : "隐藏")}（alpha = {alpha}）。" +
                  (alpha > 0f ? "调完记得点「隐藏预览」再保存场景。" : "可以保存场景了。"));
    }

    //按名字递归找子物体（立绘挂在 Canvas 下，层级可能再调整）
    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
