using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//敌人基类
public class Enemy : MonoBehaviour, IDamageable, INoiseListener
{
    public Animator anim { get; private set; }//动画组件（位于子对象上）
    public SpriteRenderer spriteRenderer { get; private set; }//精灵渲染器（位于子对象上�?
    public Rigidbody2D rb { get; private set; }//刚体
    public StateMachine stateMachine { get; private set; }//状态机实例

    public Enemy_IdleState idleState;//待机状�?
    public Enemy_MoveState moveState;//巡逻移动状�?
    public Enemy_ChaseState chaseState;//追逐状�?
    public Enemy_AttackState attackState;//攻击冲刺状�?
    public Enemy_ReturnState returnState;//返回巡逻状�?
    public Enemy_DeadState deadState;//死亡状�?
    public Enemy_InvestigateState investigateState;//调查声音状态（听到噪音时前往查看�?

    [Header("Health")]
    [SerializeField] protected float maxHealth = 1f;//最大生命�?
    public float currentHealth { get; private set; }//当前生命�?
    public bool isDead { get; private set; }//是否已经死亡

    [Header("Patrol")]
    public float moveDuration = 0.5f;//巡逻时走一格用�?
    public float idleTime = 2f;//待机时长
    public PatrolRoute patrolRoute;//巡逻路�?
    public List<Vector3> patrolPoints { get; set; }//巡逻路线点
    public bool patrolIsLoop { get; private set; }//路线是否首尾相连

    [Header("AI")]
    public Grid grid;//所在网格，留空则自动查�?
    public LayerMask obstacleMask;//障碍物层
    public float chaseDuration = 0.22f;//追逐时走一格用�?

    [Header("听觉")]
    public float investigateDuration = 0.3f;//调查噪音时走一格用时（比巡逻快、比追逐慢�?
    public Vector3Int? heardCell { get; private set; }//最近听到声音所在的格，null 表示没有

    [Header("Attack")]
    public int attackTriggerDistance = 2;//与玩家同一直线且距离为此值时发动冲刺
    public int attackDashCells = 3;//冲刺格数
    public float attackWindupDelay = 0.083f;//起手到第二帧的时间（12fps，约 1/12 秒）
    public float attackHoldTime = 0.1f;//到第二帧后原地停顿的时长
    public float attackDashDuration = 0.1f;//冲刺时走一格用�?
    public float attackAnimLength = 0.42f;//攻击动画时长，冲刺早了要等它播完
    public float attackCooldown = 0.2f;//攻击结束后的冷却时间
    public float nextAttackTime { get; private set; }//下次可攻击的时间�?
    public EnemyVision vision { get; private set; }//视野
    public GridPathfinder pathfinder { get; private set; }//寻路
    public GameObject trailEffect { get; private set; }//冲刺拖尾特效对象
    public float trailOffset = 0.45f;//拖尾相对身体后方的偏移距�?
    public Vector2Int facing { get; private set; } = Vector2Int.right;//四向朝向

    [Header("Rendering")]
    public bool invertSpriteFlip;//素材默认朝左的敌人勾上，把左右翻向反过来

    protected virtual void OnEnable()//register so other enemies can query occupancy without FindObjectsByType
    {
        EnemyRegistry.Register(this);
        NoiseSystem.Register(this);
    }

    protected virtual void OnDisable()
    {
        EnemyRegistry.Unregister(this);
        NoiseSystem.Unregister(this);
    }

    private void OnDestroy()
    {
        EnemyRegistry.Unregister(this);
        NoiseSystem.Unregister(this);
    }

    protected virtual void Awake()//获取组件
    {
        anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        Transform trailTransform = transform.Find("Animation/TrailEffect");
        if (trailTransform != null)
        {
            trailEffect = trailTransform.gameObject;
            trailEffect.transform.localPosition = new Vector3(-facing.x * trailOffset, -facing.y * trailOffset, 0f);
        }

        stateMachine = new StateMachine();
        currentHealth = maxHealth;

        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (secondFloorTilemap == null && grid != null)
        {
            Transform floor = grid.transform.Find("SecondFloor");
            if (floor != null)
                secondFloorTilemap = floor.GetComponent<Tilemap>();
        }
    }

    protected virtual void Start()//构建视野与寻路（需在网格解析之后）
    {
        vision = new EnemyVision(grid, obstacleMask);
        pathfinder = new GridPathfinder(grid, obstacleMask);
    }

    protected virtual void Update()
    {
        stateMachine.UpdateActiveState();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P))//调试：按 P 直接杀死敌�?
            TakeDamage(maxHealth);
#endif
    }

    public virtual void TakeDamage(float damage)//承受伤害
    {
        if (isDead || damage <= 0f)
            return;

        currentHealth -= damage;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    //刀 / �?/ 铁球都通过这个接口打过来，统一转成 TakeDamage
    public virtual void TakeHit(HitInfo info)
    {
        TakeDamage(info.damage);
    }

    protected virtual void Die()//死亡处理，子类可覆写
    {
        if (isDead)
            return;

        if (deadState == null)//没配死亡状态：报错并保持可受伤状态，避免变成杀不死的僵�?
        {
            Debug.LogError("敌人未配置死亡状态：" + name);
            return;
        }

        isDead = true;

        if (!stateMachine.ChangeState(deadState))//状态机未初始化或已锁死：回滚死亡标�?
            isDead = false;
    }

    public void DestroyGameObjectWithDelay(float delay)//延迟销毁自身（编辑模式下不销毁）
    {
        if (!Application.isPlaying)
            return;

        Destroy(gameObject, delay);
    }

    public bool BuildPatrolPath()//构建巡逻路线，失败返回 false
    {
        if (patrolRoute == null)//未配置路线表示该敌人不巡逻，不算错误
        {
            patrolPoints = null;
            return false;
        }

        if (patrolRoute.TryBuildPath(transform.position, out List<Vector3> points, out bool isLoop) == false)
        {
            patrolPoints = null;
            return false;
        }

        patrolPoints = points;
        patrolIsLoop = isLoop;
        return true;
    }

    public Vector3Int WorldToCell(Vector3 world)//世界坐标转格子坐�?
    {
        return grid == null ? Vector3Int.zero : grid.WorldToCell(world);
    }

    public Vector3 CellToCenter(Vector3Int cell)//格子坐标转格中心世界坐标
    {
        return grid == null ? Vector3.zero : grid.GetCellCenterWorld(cell);
    }

    public void SetFacing(Vector2Int direction)//更新四向朝向（零向量忽略�?
    {
        if (direction == Vector2Int.zero)
            return;

        facing = direction;

        if (trailEffect != null)//拖尾特效保持在身体背面；上下移动时整体转 90 �?
        {
            trailEffect.transform.localPosition = new Vector3(-direction.x * trailOffset, -direction.y * trailOffset, 0f);

            float angle = direction.x != 0 ? 0f : 90f;//左右用横向素材，上下转成纵向
            trailEffect.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer trailSprite = trailEffect.GetComponent<SpriteRenderer>();
            if (trailSprite != null)
                trailSprite.flipX = direction.x != 0 ? direction.x > 0 : direction.y > 0;//让浓的一端朝向身�?
        }

        if (spriteRenderer == null)
            return;

        if (direction.x != 0)//横向朝向才翻转；勾了 invertSpriteFlip 说明素材默认朝左
            spriteRenderer.flipX = invertSpriteFlip ? direction.x > 0 : direction.x < 0;
        else
            spriteRenderer.flipX = false;//facing up/down use front art, never mirror it
    }

    //动画名解析钩子：子类可以按朝向等情况把基名换成别�?clip（默认原样返回）
    public virtual string ResolveAnimName(string baseName) => baseName;

    // ===== 楼层：SecondFloor 有瓦片的格子算二楼，其余算一�?=====
    //敌人不能跨楼层移�?冲撞，否则会从一楼侧�?�?到二�?

    public Tilemap secondFloorTilemap { get; private set; }//二楼地面，Awake 时从 Grid 下自动找

    public int FloorOf(Vector3Int cell)//该格属于几楼
    {
        return FloorQuery.FloorOf(secondFloorTilemap, cell);//single source of truth
    }

    public bool SameFloor(Vector3Int a, Vector3Int b)//两个格子是否在同一�?
    {
        return FloorOf(a) == FloorOf(b);
    }

    public bool CanSeePlayer()//玩家是否在视野内
    {
        if (vision == null)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return false;

        Vector3Int playerCell = WorldToCell(player.transform.position);
        List<Vector2Int> visible = vision.GetVisibleCells(WorldToCell(transform.position), facing);
        return visible.Contains(new Vector2Int(playerCell.x, playerCell.y));
    }

    public virtual bool CanDetectPlayer()//玩家是否被察觉（史莱姆：3 排宽视野；石像鬼：正前方直线�?
    {
        return CanSeePlayer();
    }

    public virtual EnemyState GetAlertState()//察觉玩家后要进入的状态（史莱姆：追逐；石像鬼：蓄力�?
    {
        return chaseState;
    }

    public virtual bool IsBusy()//是否处于不可被噪音打断的状�?
    {
        return attackState != null && stateMachine.currentState == attackState;
    }

    public bool IsOnPatrolRoute()//当前格是否就在巡逻路线上
    {
        if (patrolPoints == null)
            return false;

        Vector3 position = transform.position;
        foreach (Vector3 point in patrolPoints)
        {
            if ((point - position).sqrMagnitude < 0.01f)
                return true;
        }

        return false;
    }

    public Vector3Int? FindClosestPatrolCell()//巡逻路线上离当前位置最近的格子
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || grid == null)
            return null;

        Vector3Int best = default;
        float bestDistance = float.MaxValue;
        Vector3 position = transform.position;

        foreach (Vector3 point in patrolPoints)
        {
            float distance = (point - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = grid.WorldToCell(point);
            }
        }

        return best;
    }

    public bool CellHasObstacle(Vector3Int cell)//格子是否有障碍物
    {
        return pathfinder != null && pathfinder.IsBlocked(cell);
    }

    public bool IsCellOccupied(Vector3Int cell)//格子是否被占：障碍、玩家或其他敌人
    {
        if (pathfinder != null && pathfinder.IsBlocked(cell))
            return true;

        if (EnemyRegistry.IsOccupiedByPlayer(cell, WorldToCell))//cached player lookup, no per-frame Find
            return true;

        return EnemyRegistry.IsOccupiedByOther(this, cell);//registry scan, no array allocation
    }

    public bool CanAttack()//冷却是否结束
    {
        return Time.time >= nextAttackTime;
    }

    public void BeginAttackCooldown()//攻击结束后开始冷�?
    {
        nextAttackTime = Time.time + attackCooldown;
    }

    // ===== 听觉：被噪音吸引 =====

    //听到声音：记下声音所在格，并进入"调查"状态走过去看看�?
    //�?NoiseBroadcast（见下方 BroadcastNoise）在开枪等事件时统一调用�?
    public void HearNoise(Vector3 worldPosition)
    {
        if (isDead || investigateState == null)
            return;

        //冲刺攻击中不打断，否则冲刺位移和动画会对不上
        if (IsBusy())
            return;

        //已经看见玩家时不需�?去查�?，继续追就行
        if (chaseState != null && stateMachine.currentState == chaseState && CanSeePlayer())
            return;

        heardCell = WorldToCell(worldPosition);

        //已经在调查时 ChangeState 会返�?false（同状态不切换），
        //但调查状态每帧都�?heardCell，所以目标会自动改成新位置�?
        bool wasInvestigating = stateMachine.currentState == investigateState;
        stateMachine.ChangeState(investigateState);
        if (!wasInvestigating)
            NoiseMark.Show(this);//右上角冒出问号
    }

    public void ClearHeardCell()//调查结束，忘掉这次声�?
    {
        heardCell = null;
    }

    //噪声广播：让半径内所有活着的敌人前往 position 查看�?
    //枪声、砸东西、爆炸都可以调它；声音穿墙（不做视线检查），这也符�?听觉"的直觉�?
    //�����㲥���ð뾶�����л��ŵĵ���ǰ�� position �鿴��ʵ�ּ� NoiseSystem��
    //ǹ�����Ҷ�������ը�����Ե�����������ǽ���������߼�飩������"����"��ֱ��
    public static void BroadcastNoise(Vector3 position, float radius)
    {
        NoiseSystem.Broadcast(position, radius);
    }

    //INoiseListener�����Լ����� NoiseSystem������ÿ�ι㲥�� FindObjectsByType
    public Vector3 NoisePosition => transform.position;

    public void OnHeardNoise(Vector3 worldPosition)
    {
        HearNoise(worldPosition);
    }
}

