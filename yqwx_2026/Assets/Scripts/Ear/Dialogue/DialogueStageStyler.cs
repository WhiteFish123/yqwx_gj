// 对话演出控制器：按当前这句的「说话人」自动切换立绘 / 对话框背景 / 文字区域，
// 剧本里再用行尾标签做单行微调。
//
// 实现方式：继承 Yarn 的 DialoguePresenterBase，挂在 DialogueSystem 上并加进
// DialogueRunner 的 Presenters 列表。它不是来显示文字的（文字还是 Line Presenter 管），
// 只是在每句开始时先把舞台摆好，然后立刻返回，不阻塞对话。
//
// 剧本写法：
//   薇薇安: 三年前也是这条路。          ← 自动套「薇薇安」那条样式
//   雨刷器刮过挡风玻璃。                 ← 没有说话人，套 fallback（旁白）样式
//   薇薇安: ……啧。  #portrait:right    ← 这一句立绘临时换到右边
//
// 支持的标签（写在行尾，Yarn 里就是 # 开头的元数据）：
//   #portrait: left|center|right|hide   立绘位置（hide/none/off 都等于不显示）
//   #portrait_img: key                  换立绘（key 来自样式表的 portraits 列表）
//   #portrait_off: x,y                  立绘偏移（像素）
//   #portrait_scale: 1.2                立绘缩放
//   #portrait_flip: 1                   立绘水平翻转（0 = 不翻）
//   #box: key|none                      换对话框背景（key 来自样式表的 boxes 列表）
//   #box_off: x,y                       对话框背景偏移
//   #name: left|right|center            名字靠哪边
//   #pad: 左,右,上,下                    文字区域内缩（改 VerticalLayoutGroup 的 padding）
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class DialogueStageStyler : DialoguePresenterBase
{
    [Header("引用（留空自动查找）")]
    public LinePresenter linePresenter;
    public DialogueSpeakerTable table;

    [Header("设置")]
    [Tooltip("对话结束后把立绘/背景/文字区域恢复成场景里的原始状态")]
    public bool resetOnDialogueEnd = true;

    [Tooltip("在 Console 打印每句套用的样式，调剧本时打开")]
    public bool logAppliedStyle = false;

    // ---- 运行期找到的 UI ----
    private Image portraitImage;
    private RectTransform portraitRect;
    private GameObject portraitGo;
    private Image boxImage;
    private RectTransform boxRect;
    private TextMeshProUGUI nameText;
    private VerticalLayoutGroup layoutGroup;
    private bool resolved;

    // 这一句是否要把对话框背景整个藏掉（#box:none）
    private bool hideBox;

    // 当前背景图「画面占图高」的比例（换框时更新，沿用上一句时保持）
    private float currentBandFraction = 1f;

    // ---- 场景原始状态（用于结束后恢复）----
    private Sprite orgPortraitSprite;
    private bool orgPortraitActive;
    private Vector2 orgPortraitAnchoredPos;
    private Vector2 orgPortraitAnchorMin;
    private Vector2 orgPortraitAnchorMax;
    private Vector2 orgPortraitPivot;
    private Vector3 orgPortraitScale;
    private Sprite orgBoxSprite;
    private Vector2 orgBoxAnchoredPos;
    private Vector2 orgBoxSizeDelta;
    private Vector2 orgBoxOffsetMin;
    private Vector2 orgBoxOffsetMax;
    // 背景框在 bandFraction = 1 时的高度（撑高计算以它为基准，不能改用偏移量跨度，
    // 偏移量跨度不含 Line Presenter 自身的高度，会把框算矮）
    private float orgBoxHeight;
    private TextAlignmentOptions orgNameAlignment;
    private int orgPadLeft, orgPadRight, orgPadTop, orgPadBottom;

    // 当前这句实际生效的样式（样式表的默认值 + 行内标签覆盖）
    private struct StageStyle
    {
        public PortraitSide side;
        public Sprite portrait;
        public Vector2 portraitOffset;
        public float portraitScale;
        public bool portraitFlipX;
        public TextAlignmentOptions nameAlignment;
        public Sprite box;
        public bool overrideBoxOffset;
        public Vector2 boxOffset;
        public bool overrideTextPadding;
        public Vector4 textPadding;

        public static StageStyle From(DialogueSpeakerStyle s, StageStyle fallback)
        {
            if (s == null)
                return fallback;

            return new StageStyle
            {
                side = s.portraitSide,
                portrait = s.portrait,
                portraitOffset = s.portraitOffset,
                portraitScale = s.portraitScale <= 0f ? 1f : s.portraitScale,
                portraitFlipX = s.portraitFlipX,
                nameAlignment = s.nameAlignment,
                box = s.box,
                overrideBoxOffset = s.overrideBoxOffset,
                boxOffset = s.boxOffset,
                overrideTextPadding = s.overrideTextPadding,
                textPadding = s.textPadding,
            };
        }
    }

    private void Awake()
    {
        Resolve();
    }

    private void Resolve()
    {
        if (resolved)
            return;

        if (linePresenter == null)
            linePresenter = FindFirstObjectByType<LinePresenter>();

        if (linePresenter == null)
            return;

        var root = linePresenter.transform;

        // 立绘通常放在 Line Presenter 下；但为了避免跟着对话框一起做淡入淡出
        // （半透明时会被框挡住），它可以被挪到 Line Presenter 的父级（Canvas）下。
        var portrait = root.Find("Portrait");
        if (portrait == null && root.parent != null)
            portrait = root.parent.Find("Portrait");
        if (portrait != null)
        {
            portraitGo = portrait.gameObject;
            portraitRect = portrait as RectTransform;
            portraitImage = portrait.GetComponent<Image>();
        }

        var background = root.Find("Background");
        if (background != null)
        {
            boxImage = background.GetComponent<Image>();
            boxRect = background as RectTransform;
        }

        var nameGo = root.Find("Character Name");
        if (nameGo != null)
            nameText = nameGo.GetComponent<TextMeshProUGUI>();

        layoutGroup = linePresenter.GetComponent<VerticalLayoutGroup>();

        // 记下场景里的原始状态
        if (portraitImage != null)
            orgPortraitSprite = portraitImage.sprite;
        if (portraitGo != null)
            orgPortraitActive = portraitGo.activeSelf;
        if (portraitRect != null)
        {
            orgPortraitAnchoredPos = portraitRect.anchoredPosition;
            orgPortraitAnchorMin = portraitRect.anchorMin;
            orgPortraitAnchorMax = portraitRect.anchorMax;
            orgPortraitPivot = portraitRect.pivot;
            orgPortraitScale = portraitRect.localScale;
        }
        if (boxImage != null)
            orgBoxSprite = boxImage.sprite;
        if (boxRect != null)
        {
            orgBoxAnchoredPos = boxRect.anchoredPosition;
            orgBoxSizeDelta = boxRect.sizeDelta;
            orgBoxOffsetMin = boxRect.offsetMin;
            orgBoxOffsetMax = boxRect.offsetMax;
            orgBoxHeight = boxRect.rect.height;
        }
        if (nameText != null)
            orgNameAlignment = nameText.alignment;
        if (layoutGroup != null)
        {
            orgPadLeft = layoutGroup.padding.left;
            orgPadRight = layoutGroup.padding.right;
            orgPadTop = layoutGroup.padding.top;
            orgPadBottom = layoutGroup.padding.bottom;
        }

        resolved = true;
    }

    // DialogueRunner 每句都会叫一次；这里同步摆好舞台后立刻返回，不阻塞对话推进。
    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        try
        {
            ApplyForLine(line);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DialogueStageStyler] 套用样式失败：" + e.Message, this);
        }

        return YarnTask.CompletedTask;
    }

    // 注意：基类的无参版本已标记 Obsolete，DialogueRunner 实际调用的是带 CancellationToken 的重载
    public override YarnTask OnDialogueCompleteAsync(System.Threading.CancellationToken token)
    {
        if (resetOnDialogueEnd)
            ResetToOriginal();

        return YarnTask.CompletedTask;
    }

    private void ApplyForLine(LocalizedLine line)
    {
        Resolve();
        if (linePresenter == null)
            return;

        hideBox = false;
        StageStyle style = BuildBaseStyle(line);
        ApplyMetadata(line?.Metadata, ref style);

        ApplyPortrait(style);
        ApplyBox(style);
        ApplyTextPadding(style);
        ApplyName(style);

        if (logAppliedStyle)
        {
            Debug.Log($"[DialogueStageStyler] 说话人={(string.IsNullOrEmpty(line?.CharacterName) ? "(旁白)" : line.CharacterName)}"
                      + $" 立绘={style.side} 背景={(style.box != null ? style.box.name : "(沿用)")}"
                      + $" padding=({style.textPadding.x},{style.textPadding.y},{style.textPadding.z},{style.textPadding.w})", this);
        }
    }

    private StageStyle BuildBaseStyle(LocalizedLine line)
    {
        // 先拿 fallback 当底座（旁白/没配过的说话人就用它）
        StageStyle baseStyle = StageStyle.From(table != null ? table.fallback : null, new StageStyle
        {
            side = PortraitSide.Left,
            portraitScale = 1f,
            nameAlignment = TextAlignmentOptions.TopLeft,
            textPadding = new Vector4(528f, 40f, -72f, 32f),
        });

        if (table == null)
            return baseStyle;

        string speaker = line?.CharacterName;
        var speakerStyle = table.Find(speaker);
        return speakerStyle != null ? StageStyle.From(speakerStyle, baseStyle) : baseStyle;
    }

    private void ApplyMetadata(string[] metadata, ref StageStyle style)
    {
        if (metadata == null || metadata.Length == 0)
            return;

        foreach (string raw in metadata)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string key;
            string value;
            int colon = raw.IndexOf(':');
            if (colon < 0)
            {
                key = raw.Trim().ToLowerInvariant();
                value = "";
            }
            else
            {
                key = raw.Substring(0, colon).Trim().ToLowerInvariant();
                value = raw.Substring(colon + 1).Trim();
            }

            switch (key)
            {
                case "portrait":
                    style.side = ParseSide(value, style.side);
                    break;

                case "portrait_img":
                case "portrait_image":
                {
                    var sprite = table != null ? table.FindPortrait(value) : null;
                    if (sprite != null)
                        style.portrait = sprite;
                    break;
                }

                case "portrait_off":
                    if (TryParseVector2(value, out var pOff))
                        style.portraitOffset = pOff;
                    break;

                case "portrait_scale":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale > 0f)
                        style.portraitScale = scale;
                    break;

                case "portrait_flip":
                    style.portraitFlipX = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;

                case "box":
                    if (value.Equals("none", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        hideBox = true;
                    }
                    else
                    {
                        var boxSprite = table != null ? table.FindBox(value) : null;
                        if (boxSprite != null)
                        {
                            style.box = boxSprite;
                            hideBox = false;
                        }
                    }
                    break;

                case "box_off":
                    if (TryParseVector2(value, out var bOff))
                    {
                        style.overrideBoxOffset = true;
                        style.boxOffset = bOff;
                    }
                    break;

                case "name":
                    style.nameAlignment = ParseNameAlign(value, style.nameAlignment);
                    break;

                case "pad":
                    if (TryParseVector4(value, out var pad))
                    {
                        style.overrideTextPadding = true;
                        style.textPadding = pad;
                    }
                    break;
            }
        }
    }

    private static PortraitSide ParseSide(string value, PortraitSide fallback)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "left": return PortraitSide.Left;
            case "center":
            case "middle": return PortraitSide.Center;
            case "right": return PortraitSide.Right;
            case "hide":
            case "none":
            case "off": return PortraitSide.Hide;
            default: return fallback;
        }
    }

    private static TextAlignmentOptions ParseNameAlign(string value, TextAlignmentOptions fallback)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "left": return TextAlignmentOptions.TopLeft;
            case "center":
            case "middle": return TextAlignmentOptions.Top;
            case "right": return TextAlignmentOptions.TopRight;
            default: return fallback;
        }
    }

    private static bool TryParseVector2(string value, out Vector2 result)
    {
        result = Vector2.zero;
        var parts = value.Split(',');
        if (parts.Length != 2)
            return false;

        if (!float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float x))
            return false;
        if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float y))
            return false;

        result = new Vector2(x, y);
        return true;
    }

    private static bool TryParseVector4(string value, out Vector4 result)
    {
        result = Vector4.zero;
        var parts = value.Split(',');
        if (parts.Length != 4)
            return false;

        var v = new float[4];
        for (int i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v[i]))
                return false;
        }

        result = new Vector4(v[0], v[1], v[2], v[3]);
        return true;
    }

    private void ApplyPortrait(StageStyle style)
    {
        if (portraitGo == null)
            return;

        if (style.side == PortraitSide.Hide)
        {
            portraitGo.SetActive(false);
            return;
        }

        portraitGo.SetActive(true);

        if (portraitImage != null && style.portrait != null)
            portraitImage.sprite = style.portrait;

        if (portraitRect == null)
            return;

        Vector2 anchor = style.side switch
        {
            PortraitSide.Left => new Vector2(0f, 0.5f),
            PortraitSide.Right => new Vector2(1f, 0.5f),
            _ => new Vector2(0.5f, 0.5f),
        };

        portraitRect.anchorMin = anchor;
        portraitRect.anchorMax = anchor;
        portraitRect.pivot = new Vector2(0.5f, 0.5f);

        // 原始位置是「靠左」的：x 从左边量；靠右时把 x 反过来从右边量；居中时从中间量。
        float baseX = style.side switch
        {
            PortraitSide.Left => orgPortraitAnchoredPos.x,
            PortraitSide.Right => -orgPortraitAnchoredPos.x,
            _ => 0f,
        };

        portraitRect.anchoredPosition = new Vector2(
            baseX + style.portraitOffset.x,
            orgPortraitAnchoredPos.y + style.portraitOffset.y);

        // 相对场景原始缩放做倍率，1 = 保持原样
        float scale = style.portraitScale <= 0f ? 1f : style.portraitScale;
        float sx = orgPortraitScale.x * scale;
        float sy = orgPortraitScale.y * scale;
        portraitRect.localScale = new Vector3(style.portraitFlipX ? -sx : sx, sy, orgPortraitScale.z);
    }

    private void ApplyBox(StageStyle style)
    {
        if (boxImage == null)
            return;

        if (hideBox)
        {
            boxImage.enabled = false;
            hideBox = false;
            return;
        }

        boxImage.enabled = true;

        if (style.box != null)
            boxImage.sprite = style.box;

        if (boxRect == null)
            return;

        // 每张框都可以有自己的矩形：先取场景原始矩形，再按这一句的框做覆盖
        Vector2 min = orgBoxOffsetMin;
        Vector2 max = orgBoxOffsetMax;

        // 换背景时按「画面占图高比例」把框撑高，让画面正好贴住文字区（像素图上方有留白的框会用到）
        // 这一句没指定背景（沿用上一句的图）时，继续用上一句的比例，不能退回 1
        float bandFraction = currentBandFraction;
        if (style.box != null)
        {
            bandFraction = 1f;
            if (table != null)
            {
                var entry = table.FindBoxEntry(style.box);
                if (entry != null)
                {
                    if (entry.overrideRect)
                    {
                        // 这张框自己定了矩形（等同 Inspector 里 stretch 下的 Left/Top/Right/Bottom）
                        min = new Vector2(entry.rectLeft, entry.rectBottom);
                        max = new Vector2(entry.rectRight, entry.rectTop);
                    }
                    else if (entry.bandFraction > 0.05f)
                    {
                        bandFraction = Mathf.Clamp(entry.bandFraction, 0.05f, 1f);
                    }
                }
            }
            currentBandFraction = bandFraction;
        }

        if (bandFraction < 0.999f)
        {
            // 以“band = 1 时框的高度”为基准撑高：框高 = 基准高 / band，
            // 这样只有底部 band 比例不透明的图（如 Layer 4_0 的 0.637）撑完后，
            // 可见画面的高度与普通框一致。
            float baseHeight = orgBoxHeight > 0f ? orgBoxHeight : (max.y - min.y);
            float delta = baseHeight * (1f / bandFraction - 1f);
            max.y += delta;
        }

        if (style.overrideBoxOffset)
        {
            min += style.boxOffset;
            max += style.boxOffset;
        }

        boxRect.offsetMin = min;
        boxRect.offsetMax = max;
    }

    private void ApplyTextPadding(StageStyle style)
    {
        if (layoutGroup == null || !style.overrideTextPadding)
            return;

        var pad = layoutGroup.padding;
        pad.left = Mathf.RoundToInt(style.textPadding.x);
        pad.right = Mathf.RoundToInt(style.textPadding.y);
        pad.top = Mathf.RoundToInt(style.textPadding.z);
        pad.bottom = Mathf.RoundToInt(style.textPadding.w);
        layoutGroup.padding = pad;

        // padding 改了要强制重排一次，否则这一句还会沿用上一句的位置
        LayoutRebuilder.ForceRebuildLayoutImmediate(linePresenter.transform as RectTransform);
    }

    private void ApplyName(StageStyle style)
    {
        if (nameText == null)
            return;

        // 名字的对齐方式跟着说话人走：立绘在左边时名字靠左，立绘在右边时名字靠右
        nameText.alignment = style.nameAlignment;
    }

    private void ResetToOriginal()
    {
        Resolve();

        if (portraitImage != null)
            portraitImage.sprite = orgPortraitSprite;
        if (portraitGo != null)
            portraitGo.SetActive(orgPortraitActive);
        if (portraitRect != null)
        {
            portraitRect.anchorMin = orgPortraitAnchorMin;
            portraitRect.anchorMax = orgPortraitAnchorMax;
            portraitRect.pivot = orgPortraitPivot;
            portraitRect.anchoredPosition = orgPortraitAnchoredPos;
            portraitRect.localScale = orgPortraitScale;
        }

        if (boxImage != null)
        {
            boxImage.sprite = orgBoxSprite;
            boxImage.enabled = true;
        }
        if (boxRect != null)
        {
            boxRect.offsetMin = orgBoxOffsetMin;
            boxRect.offsetMax = orgBoxOffsetMax;
            boxRect.anchoredPosition = orgBoxAnchoredPos;
            boxRect.sizeDelta = orgBoxSizeDelta;
        }

        if (nameText != null)
            nameText.alignment = orgNameAlignment;

        if (layoutGroup != null)
        {
            var pad = layoutGroup.padding;
            pad.left = orgPadLeft;
            pad.right = orgPadRight;
            pad.top = orgPadTop;
            pad.bottom = orgPadBottom;
            layoutGroup.padding = pad;
            LayoutRebuilder.ForceRebuildLayoutImmediate(linePresenter.transform as RectTransform);
        }

        hideBox = false;
        currentBandFraction = 1f;
    }
}

