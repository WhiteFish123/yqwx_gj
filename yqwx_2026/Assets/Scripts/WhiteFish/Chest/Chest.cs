using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//可被抓取推拉的箱子：维护自身格子坐标、全局占用查询、逐格插值与点燃燃烧状态
public class Chest : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空自动查找
    [SerializeField] private GameObject firePosition;//火焰子物体，留空自动查找
    [SerializeField] private SpriteRenderer bodyRenderer;//箱体渲染器，留空自动查找
    [SerializeField] private float burnDuration = 5f;//燃烧持续时间（秒）
    [SerializeField] private Color charredColor = new Color(0.25f, 0.22f, 0.20f, 1f);//烧完后的焦黑色

    [Header("楼层")]
    [SerializeField] private Tilemap secondFloorTilemap;//二楼地面，留空自动查找 Grid 下的 SecondFloor
    [SerializeField] private Tilemap floorTilemap;//普通地面，留空自动查找 Grid 下的 Floor
    [SerializeField] private float fallDuration = 0.2f;//掉落每格用时（秒）
    [SerializeField] private int maxFallCells = 50;//最多连续下落的格数，防止无限下落

    [Header("燃烧噪音")]
    [Tooltip("燃烧时向周围广播噪音的半径，半径内的敌人会走过来查看；0 = 无声")]
    [SerializeField] private float burnNoiseRadius = 8f;
    [Tooltip("燃烧期间隔多久广播一次噪音（秒）")]
    [SerializeField] private float burnNoiseInterval = 0.5f;

    [Header("掉落伤害")]
    [Tooltip("落地时以落点为中心 3×3 范围造成的伤害，0 = 不造成伤害")]
    [SerializeField] private float fallDamage = 10f;

    private static readonly List<Chest> activeChests = new List<Chest>();

    private Vector2Int cell;//逻辑格子坐标
    private Animator fireAnimator;//火焰动画组件
    private float noiseTimer;//燃烧噪音的倒计时

    public Vector2Int Cell => cell;
    public bool IsMoving { get; private set; }
    public bool IsFalling { get; private set; }
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

        if ((secondFloorTilemap == null || floorTilemap == null) && grid != null)//在同一个 Grid 下按名字补齐楼层引用
        {
            Transform host = grid.transform;

            if (secondFloorTilemap == null)
            {
                Transform sf = host.Find("SecondFloor");
                if (sf != null)
                    secondFloorTilemap = sf.GetComponent<Tilemap>();
            }

            if (floorTilemap == null)
            {
                Transform fl = host.Find("Floor");
                if (fl != null)
                    floorTilemap = fl.GetComponent<Tilemap>();
            }
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

        //刚点燃就喊一嗓子，让附近敌人立刻注意到
        BroadcastBurnNoise();
    }

    //燃烧噪音：把"着火了"这件事告诉半径内的敌人，它们会循声过来查看
    private void BroadcastBurnNoise()
    {
        if (burnNoiseRadius <= 0f)
            return;

        Enemy.BroadcastNoise(transform.position, burnNoiseRadius);
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

        //燃烧途中持续喊，让新走进范围的敌人也能被吸引过来
        if (burnNoiseRadius > 0f)
        {
            noiseTimer -= deltaTime;

            if (noiseTimer <= 0f)
            {
                noiseTimer = Mathf.Max(0.05f, burnNoiseInterval);
                BroadcastBurnNoise();
            }
        }

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

    //判定某格是不是二楼地面：SecondFloor 上有瓦片就是二楼
    public bool IsSecondFloorCell(Vector2Int target)
    {
        if (secondFloorTilemap == null)
            return false;

        return secondFloorTilemap.HasTile(new Vector3Int(target.x, target.y, 0));
    }

    //从二楼被推到非二楼格子时需要掉落
    public bool ShouldFall(Vector2Int target)
    {
        return IsSecondFloorCell(cell) && !IsSecondFloorCell(target);
    }

    //从当前格向下找第一个普通地面格，途中的墙和其他箱子直接穿过
    public Vector2Int FindLandingCell()
    {
        if (floorTilemap == null || grid == null)
            return cell;

        Vector2Int landing = cell;

        for (int i = 0; i < maxFallCells; i++)
        {
            Vector2Int next = new Vector2Int(landing.x, landing.y - 1);

            if (floorTilemap.HasTile(new Vector3Int(next.x, next.y, 0)))//碰到地面就停下
                return next;

            landing = next;
        }

        return landing;
    }

    //逐格向下掉落，每格用时 fallDuration
    public void FallTo(Vector2Int target)
    {
        if (grid == null || target == cell)
            return;

        Vector2Int start = cell;
        cell = target;//逻辑格子立刻更新，占用查询当场生效
        StartCoroutine(FallRoutine(start, target));
    }

    private IEnumerator FallRoutine(Vector2Int start, Vector2Int target)
    {
        IsFalling = true;

        int steps = start.y - target.y;

        for (int i = 0; i < steps; i++)
        {
            Vector3 from = transform.position;
            Vector3 to = grid.GetCellCenterWorld(new Vector3Int(target.x, start.y - i - 1, 0));

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, fallDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            transform.position = to;
        }

        IsFalling = false;

        DealFallDamage();//落地砸一下，3×3 范围内都吃伤害
    }

    //落点为中心 3×3 范围伤害：范围内所有 IDamageable 吃一份，敌人会因此播死亡动画
    private void DealFallDamage()
    {
        if (fallDamage <= 0f || grid == null)
            return;

        //玩家/敌人可能刚被挪过位置，先把 Transform 同步进物理世界，否则 OverlapBox 会扫空
        Physics2D.SyncTransforms();

        Vector2 size = grid.cellSize * 0.8f;//缩到 0.8 倍，避免相邻格边缘误伤

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int target2 = new Vector2Int(cell.x + dx, cell.y + dy);

                //目标还站在二楼地面上：箱子是从二楼掉下去的，砸不到同层的东西
                if (IsSecondFloorCell(target2))
                    continue;

                Vector3Int target = new Vector3Int(target2.x, target2.y, 0);
                Vector3 center = grid.GetCellCenterWorld(target);
                Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);

                foreach (Collider2D hit in hits)
                {
                    IDamageable damageable = hit.GetComponent<IDamageable>();
                    if (damageable == null)
                        continue;

                    HitInfo info = new HitInfo();
                    info.damage = fallDamage;
                    info.type = HitType.ironBall;//砸击，和铁球同类

                    damageable.TakeHit(info);
                }
            }
        }
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
