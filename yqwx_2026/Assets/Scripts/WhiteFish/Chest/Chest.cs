using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//可被抓取推拉的箱子：维护自身格子坐标、全局占用查询、逐格插值与点燃燃烧状态
public class Chest : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空自动查找
    [SerializeField] private GameObject firePosition;//火焰子物体，留空自动查找
    [SerializeField] private SpriteRenderer bodyRenderer;//箱体渲染器，留空自动查找
    [SerializeField] private float burnDuration = 5f;//燃烧持续时间（秒）
    [SerializeField] private Color charredColor = new Color(0.25f, 0.22f, 0.20f, 1f);//烧完后的焦黑色

    private static readonly List<Chest> activeChests = new List<Chest>();

    private Vector2Int cell;//逻辑格子坐标
    private Animator fireAnimator;//火焰动画组件

    public Vector2Int Cell => cell;
    public bool IsMoving { get; private set; }
    public bool IsBurning { get; private set; }
    public bool IsCharred { get; private set; }
    public float BurnRemaining { get; private set; }

    public event Action<Chest> Ignited;//点燃那一刻
    public event Action<Chest> BurnFinished;//烧完转焦黑那一刻

    public static Chest At(Vector2Int target)//查询占用某格的箱子，无则返回 null
    {
        foreach (Chest chest in activeChests)
        {
            if (chest != null && chest.cell == target)
                return chest;
        }

        return null;
    }

    private void Awake()//先于 OnEnable 注册，保证进表时格子已正确
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid != null)
            cell = (Vector2Int)grid.WorldToCell(transform.position);

        if (firePosition == null)
        {
            Transform found = transform.Find("FirePosition");
            if (found != null)
                firePosition = found.gameObject;
        }

        if (firePosition != null)
        {
            firePosition.SetActive(false);
            fireAnimator = firePosition.GetComponent<Animator>();
        }

        if (bodyRenderer == null)
        {
            Transform body = transform.Find("Animation");
            if (body != null)
                bodyRenderer = body.GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        if (!activeChests.Contains(this))
            activeChests.Add(this);
    }

    private void OnDisable()
    {
        activeChests.Remove(this);
    }

    public void Ignite()//点燃箱子，已燃烧或已焦黑则忽略
    {
        if (IsBurning || IsCharred)
            return;

        IsBurning = true;
        BurnRemaining = burnDuration;

        if (firePosition != null)
        {
            firePosition.SetActive(true);

            if (fireAnimator != null)
                fireAnimator.Play("Fire", 0, 0f);
        }

        Ignited?.Invoke(this);
    }

    private void Update()
    {
        if (IsBurning)
            Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)//推进燃烧计时，归零时转为焦黑
    {
        if (!IsBurning)
            return;

        BurnRemaining -= deltaTime;

        if (BurnRemaining > 0f)
            return;

        BurnRemaining = 0f;
        IsBurning = false;
        IsCharred = true;

        if (firePosition != null)
            firePosition.SetActive(false);

        if (bodyRenderer != null)
            bodyRenderer.color = charredColor;

        BurnFinished?.Invoke(this);
    }

    public void MoveTo(Vector2Int target, float duration)//逻辑位置先行，视觉上插值跟上
    {
        cell = target;
        StartCoroutine(MoveRoutine(target, duration));
    }

    private IEnumerator MoveRoutine(Vector2Int target, float duration)
    {
        IsMoving = true;

        Vector3 start = transform.position;
        Vector3 end = grid.GetCellCenterWorld(new Vector3Int(target.x, target.y, 0));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        transform.position = end;
        IsMoving = false;
    }
}
