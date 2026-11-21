// 对话演出样式表：一个说话人一套「立绘 / 对话框背景 / 文字区域」的默认样式。
// 剧本里正常写 "薇薇安: ..." 就会自动套用，不用每行打标签；
// 个别行想临时改，用行内元数据（行尾加 #标签）覆盖，见 DialogueStageStyler。
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>立绘站在对话框的哪一侧。</summary>
public enum PortraitSide
{
    Hide = 0,    // 不显示立绘
    Left = 1,    // 左侧（默认，原布局）
    Center = 2,  // 居中
    Right = 3,   // 右侧（水平镜像位置）
}

/// <summary>行内标签可以按名字引用的图片（#portrait_img:key / #box:key）。</summary>
[Serializable]
public class NamedSprite
{
    public string key;
    public Sprite sprite;

    [Tooltip("对话框背景专用：画面（不透明部分）占整张图高度的比例。"
             + "1 = 整张图都是框；像素图上方留白时要填实际比例，程序会把框撑高，让画面正好贴住文字区。")]
    [Range(0.05f, 1f)]
    public float bandFraction = 1f;

    [Tooltip("对话框背景专用：勾上后不再按比例撑高，直接使用下面四个数值作为这一张框的矩形"
             + "（含义与 Inspector 里 stretch 锚点下的 Left / Top / Right / Bottom 完全一致），"
             + "这样同一个 Background 物体可以让不同的框各用各的大小位置。")]
    public bool overrideRect;

    [Header("框的矩形（仅 overrideRect 勾上时生效）")]
    public float rectLeft;
    public float rectTop;
    public float rectRight;
    public float rectBottom;
}

/// <summary>一个说话人的演出样式。</summary>
[Serializable]
public class DialogueSpeakerStyle
{
    [Header("说话人（Yarn 里 : 前面的名字，可留空表示旁白）")]
    public string speakerName = "";

    [Header("立绘")]
    [Tooltip("立绘放在哪一侧；Hide = 这句不显示立绘")]
    public PortraitSide portraitSide = PortraitSide.Left;

    [Tooltip("立绘图片；留空 = 沿用上一句的立绘（只换位置不换图）")]
    public Sprite portrait;

    [Tooltip("立绘相对默认位置的偏移（像素，右下为正）")]
    public Vector2 portraitOffset = Vector2.zero;

    [Tooltip("立绘缩放，1 = 原尺寸")]
    public float portraitScale = 1f;

    [Tooltip("水平翻转立绘")]
    public bool portraitFlipX;

    [Header("名字")]
    [Tooltip("名字的水平对齐：默认 TopLeft；立绘放右边时一般用 TopRight")]
    public TextAlignmentOptions nameAlignment = TextAlignmentOptions.TopLeft;

    [Header("对话框背景")]
    [Tooltip("对话框背景图；留空 = 沿用上一句的背景图")]
    public Sprite box;

    [Tooltip("勾上才会用下面的偏移改背景位置")]
    public bool overrideBoxOffset;
    public Vector2 boxOffset = Vector2.zero;

    [Header("文字区域")]
    [Tooltip("勾上才会改文字区域（改的是 Line Presenter 上 VerticalLayoutGroup 的 padding）")]
    public bool overrideTextPadding;

    [Tooltip("文字区域内缩：左、右、上、下（像素，和 VerticalLayoutGroup 的 Padding 一致）")]
    public Vector4 textPadding = new Vector4(528f, 40f, -72f, 32f);
}

/// <summary>
/// 说话人样式表。放在 Assets/Data/Dialogue 下，由 DialogueStageStyler 引用。
/// </summary>
[CreateAssetMenu(menuName = "对话/说话人样式表", fileName = "DialogueSpeakerTable")]
public class DialogueSpeakerTable : ScriptableObject
{
    [Tooltip("每个说话人一条；名字和剧本里 : 前面的一致（不区分大小写、忽略前后空格）")]
    public List<DialogueSpeakerStyle> speakers = new List<DialogueSpeakerStyle>();

    [Tooltip("没有说话人的旁白、或者名字没配过的说话人，用这一条")]
    public DialogueSpeakerStyle fallback = new DialogueSpeakerStyle
    {
        speakerName = "(旁白/默认)",
        portraitSide = PortraitSide.Hide,
        overrideTextPadding = true,
        // 没有立绘时文字可以往左铺开
        textPadding = new Vector4(120f, 40f, -72f, 32f),
    };

    [Header("行内标签可引用的图片")]
    [Tooltip("#portrait_img:key 用的立绘图")]
    public List<NamedSprite> portraits = new List<NamedSprite>();

    [Tooltip("#box:key 用的对话框背景图")]
    public List<NamedSprite> boxes = new List<NamedSprite>();

    /// <summary>按说话人名字找样式；找不到返回 null（调用方自己决定用 fallback）。</summary>
    public DialogueSpeakerStyle Find(string speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
            return null;

        string key = speakerName.Trim();
        foreach (var style in speakers)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.speakerName))
                continue;

            if (string.Equals(style.speakerName.Trim(), key, StringComparison.OrdinalIgnoreCase))
                return style;
        }

        return null;
    }

    public Sprite FindPortrait(string key) => FindNamed(portraits, key);

    public Sprite FindBox(string key) => FindNamed(boxes, key);

    /// <summary>反查这张背景图在 boxes 列表里对应的条目（用来拿 bandFraction）。</summary>
    public NamedSprite FindBoxEntry(Sprite sprite)
    {
        if (boxes == null || sprite == null)
            return null;

        foreach (var entry in boxes)
        {
            if (entry != null && entry.sprite == sprite)
                return entry;
        }

        return null;
    }

    private static Sprite FindNamed(List<NamedSprite> list, string key)
    {
        if (list == null || string.IsNullOrWhiteSpace(key))
            return null;

        string wanted = key.Trim();
        foreach (var entry in list)
        {
            if (entry != null && entry.sprite != null
                && string.Equals(entry.key?.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }

        return null;
    }
}
