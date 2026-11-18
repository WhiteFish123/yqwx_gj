using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//可被抓取推拉的箱子：维护自身格子坐标、全局占用查询、逐格插值与点燃燃烧状�?
public class Chest : MonoBehaviour, INoiseSource
{
    [SerializeField] private Grid grid;//所在网格，留空自动查找
    [SerializeField] private GameObject firePosition;//火焰子物体，留空自动查找
    [SerializeField] private SpriteRenderer bodyRenderer;//箱体渲染器，留空自动查找
    [SerializeField] private float burnDuration = 10f;//燃烧持续时间（秒�?
    [SerializeField] private Color charredColor = new Color(0.25f, 0.22f, 0.20f, 1f);//烧完后的焦黑�?

    [Header("楼层")]
    [SerializeField] private Tilemap secondFloorTilemap;//二楼地面，留空自动查�?Grid 下的 SecondFloor
    [SerializeField] private Tilemap floorTilemap;//普通地面，留空自动查找 Grid 下的 Floor
    [SerializeField] private float fallDuration = 0.2f;//掉落每格用时（秒�?
    [SerializeField] private int fallCells = 1;//掉落向下偏移的格数（= 两层楼在屏幕上的高度差），遇阻即停

    [Header("燃烧噪音")]
    [Tooltip("燃烧时向周围广播噪音的半径，半径内的敌人会走过来查看�? = 无声")]
    [SerializeField] private float burnNoiseRadius = 12f;
    [Tooltip("燃烧期间隔多久广播一次噪音（秒）")]
    [SerializeField] private float burnNoiseInterval = 0.5f;

    [Header("掉落伤害")]
    [Tooltip("落地时造成的伤害，0 = 不造成伤害")]
    [SerializeField] private float fallDamage = 10f;
    [Tooltip("沿推动方向的格数（箱子在起点，往前延伸）")]
    [SerializeField] private int fallDamageAlong = 3;
    [Tooltip("垂直于推动方向的格数（箱子在正中间）")]
    [SerializeField] private int fallDamageAcross = 5;

    private static readonly List<Chest> activeChests = new List<Chest>();

    private Vector2Int cell;//逻辑格子坐标
    private Animator fireAnimator;//火焰动画组件
    private float noiseTimer;//燃烧噪音的倒计�?

    public Vector2Int Cell => cell;
    public bool IsMoving { get; private set; }
    public bool IsFalling { get; private set; }
    public bool IsBusy => IsMoving || IsFalling;//while moving or falling the chest cannot be grabbed or pushed
    public bool IsBurning { get; private set; }

    //INoiseSource：燃烧时一直在发出噪音，敌人会守在旁边等它熄灭
    public bool IsNoiseActive => IsBurning;

    //INoiseSource: false once this object is destroyed, so listeners stop watching
    public bool IsAlive => this != null;

    private Vector2Int fallPushDirection = Vector2Int.right;//掉落伤害矩形的朝向（沿推动方向）

    //推箱时记录推动方向，掉落伤害按这个方向摆矩形
    public void SetFallPushDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return;

        fallPushDirection = direction;
    }
    public bool IsCharred { get; private set; }
    public float BurnRemaining { get; private set; }

    public event Action<Chest> Ignited;//点燃那一�?
    public event Action<Chest> BurnFinished;//烧完转焦黑那一�?

    public static Chest At(Vector2Int target)//查询占用某格的箱子，无则返回 null
    {
        foreach (Chest chest in activeChests)
        {
            if (chest != null && chest.cell == target)
                return chest;
        }

        return null;
    }

    private void Awake()//先于 OnEnable 注册，保证进表时格子已正�?
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

        if ((secondFloorTilemap == null || floorTilemap == null) && grid != null)//在同一�?Grid 下按名字补齐楼层引用
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

    public void Ignite()//点燃箱子，已燃烧或已焦黑则忽�?
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

        //刚点燃就喊一嗓子，让附近敌人立刻注意�?
        BroadcastBurnNoise();
    }

    //燃烧噪音：把"着火了"这件事告诉半径内的敌人，它们会循声过来查�?
    private void BroadcastBurnNoise()
    {
        if (burnNoiseRadius <= 0f)
            return;

        NoiseSystem.Broadcast(transform.position, burnNoiseRadius);
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

    public void MoveTo(Vector2Int target, float duration)//逻辑位置先行，视觉上插值跟�?
    {
        if (IsBusy) return;//already moving or falling, ignore the new request
        cell = target;
        StartCoroutine(MoveRoutine(target, duration));
    }

    //判定某格是不是二楼地面：SecondFloor 上有瓦片就是二楼
    public bool IsSecondFloorCell(Vector2Int target)
    {
        return FloorQuery.IsSecondFloor(secondFloorTilemap, new Vector3Int(target.x, target.y, 0));//single source of truth
    }

    //从二楼被推到非二楼格子时需要掉�?
    public bool ShouldFall(Vector2Int target)
    {
        return IsSecondFloorCell(cell) && !IsSecondFloorCell(target);
    }

    //从当前格向下偏移 fallCells 格作为落点：距离固定，不随地图瓦片变化；遇阻即停
    public Vector2Int FindLandingCell()
    {
        if (floorTilemap == null || grid == null)
            return cell;

        Vector3Int landing = GridRules.FallLanding(
            new Vector3Int(cell.x, cell.y, 0), fallCells, IsFreeLandingCell);

        return new Vector2Int(landing.x, landing.y);
    }

    //落点必须是一楼地板、不是二楼，也没有别的箱子
    private bool IsFreeLandingCell(Vector3Int target)
    {
        if (!floorTilemap.HasTile(target))
            return false;

        if (secondFloorTilemap != null && secondFloorTilemap.HasTile(target))
            return false;

        return Chest.At(new Vector2Int(target.x, target.y)) == null;
    }

    //逐格向下掉落，每格用 fallDuration
    public void FallTo(Vector2Int target)
    {
        if (IsBusy || grid == null || target == cell)
            return;

        Vector2Int start = cell;
        cell = target;//逻辑格子立刻更新，占用查询当场生�?
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

        DealFallDamage();//落地砸一下，3×3 范围内都吃伤�?
    }

    //Damages a rectangle around the landing cell: fallDamageAlong cells along the push direction (chest at the near edge), fallDamageAcross cells across (chest centered)
    private void DealFallDamage()
    {
        if (fallDamage <= 0f || grid == null)
            return;

        //玩家/敌人可能刚被挪过位置，先�?Transform 同步进物理世界，否则 OverlapBox 会扫�?
        Physics2D.SyncTransforms();

        Vector2 size = grid.cellSize * 0.8f;//缩到 0.8 倍，避免相邻格边缘误�?

        Vector2Int dir = fallPushDirection;
        Vector2Int side = new Vector2Int(-dir.y, dir.x);

        int along = Mathf.Max(1, fallDamageAlong);
        int across = Mathf.Max(1, fallDamageAcross);
        int acrossStart = -(across / 2);

        for (int a = 0; a < along; a++)
        {
            for (int b = acrossStart; b < acrossStart + across; b++)
            {
                Vector2Int target2 = new Vector2Int(cell.x + dir.x * a + side.x * b, cell.y + dir.y * a + side.y * b);

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
