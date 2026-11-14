using System.Collections.Generic;
using UnityEngine;

//按世界 Y 坐标动态设置 sortingOrder：y 越小越靠前，用于 2.5D 遮挡
public class YSort : MonoBehaviour
{
    [SerializeField] private float yOffset = 0f;//手动微调，正数让实体更靠前
    [SerializeField] private int precision = 100;//Y 量化倍率：1 个世界单位 = 100 档
    [SerializeField] private int baseOrder = 10000;//基础层，必须远高于 Tilemap 的 -10/-1
    [SerializeField] private bool showDebug;//调试开关

    private struct Entry
    {
        public SpriteRenderer renderer;
        public int baseOffset;
    }

    private readonly List<Entry> entries = new List<Entry>();

    private void Awake()
    {
        Refresh();
    }

    public void Refresh()//重新收集子 SpriteRenderer，已收集过的沿用首次偏移
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogError("[YSort] " + name + " 没有任何 SpriteRenderer，排序不会生效", this);
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!Contains(renderers[i]))
                entries.Add(new Entry { renderer = renderers[i], baseOffset = renderers[i].sortingOrder });
        }
    }

    private bool Contains(SpriteRenderer renderer)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].renderer == renderer)
                return true;
        }

        return false;
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (entries.Count == 0)
            return;

        int entityOrder = baseOrder
            - Mathf.RoundToInt(transform.position.y * precision)
            + Mathf.RoundToInt(yOffset * precision);

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].renderer == null)
                continue;

            entries[i].renderer.sortingOrder = entityOrder + entries[i].baseOffset;
        }

        if (showDebug)
            Debug.Log("[YSort] " + name + " y=" + transform.position.y + " order=" + entityOrder);
    }
}
