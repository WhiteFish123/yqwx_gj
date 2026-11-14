using System.Collections.Generic;
using UnityEngine;

//敌人基类
public class Enemy : MonoBehaviour
{
    public Animator anim { get; private set; }//动画组件（位于子对象上）
    public SpriteRenderer spriteRenderer { get; private set; }//精灵渲染器（位于子对象上）
    public Rigidbody2D rb { get; private set; }//刚体
    public StateMachine stateMachine { get; private set; }//状态机实例

    public Enemy_IdleState idleState;//待机状态
    public Enemy_MoveState moveState;//巡逻移动状态
    public Enemy_ChaseState chaseState;//追逐状态
    public Enemy_AttackState attackState;//攻击冲刺状态
    public Enemy_ReturnState returnState;//返回巡逻状态
    public Enemy_DeadState deadState;//死亡状态

    [Header("Health")]
    [SerializeField] protected float maxHealth = 1f;//最大生命值
    public float currentHealth { get; private set; }//当前生命值
    public bool isDead { get; private set; }//是否已经死亡

    [Header("Patrol")]
    public float moveDuration = 0.5f;//巡逻时走一格用时
    public float idleTime = 2f;//待机时长
    public PatrolRoute patrolRoute;//巡逻路线
    public List<Vector3> patrolPoints { get; set; }//巡逻路线点
    public bool patrolIsLoop { get; private set; }//路线是否首尾相连

    [Header("AI")]
    public Grid grid;//所在网格，留空则自动查找
    public LayerMask obstacleMask;//障碍物层
    public float chaseDuration = 0.22f;//追逐时走一格用时

    [Header("Attack")]
    public int attackTriggerDistance = 2;//与玩家同一直线且距离为此值时发动冲刺
    public int attackDashCells = 3;//冲刺格数
    public float attackWindupDelay = 0.083f;//起手到第二帧的时间（12fps，约 1/12 秒）
    public float attackHoldTime = 0.1f;//到第二帧后原地停顿的时长
    public float attackDashDuration = 0.1f;//冲刺时走一格用时
    public float attackAnimLength = 0.42f;//攻击动画时长，冲刺早了要等它播完
    public float attackCooldown = 0.2f;//攻击结束后的冷却时间
    public float nextAttackTime { get; private set; }//下次可攻击的时间点
    public EnemyVision vision { get; private set; }//视野
    public GridPathfinder pathfinder { get; private set; }//寻路
    public GameObject trailEffect { get; private set; }//冲刺拖尾特效对象
    public float trailOffset = 0.45f;//拖尾相对身体后方的偏移距离
    public Vector2Int facing { get; private set; } = Vector2Int.right;//四向朝向

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
        if (Input.GetKeyDown(KeyCode.P))//调试：按 P 直接杀死敌人
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

    protected virtual void Die()//死亡处理，子类可覆写
    {
        if (isDead)
            return;

        if (deadState == null)//没配死亡状态：报错并保持可受伤状态，避免变成杀不死的僵尸
        {
            Debug.LogError("敌人未配置死亡状态：" + name);
            return;
        }

        isDead = true;

        if (!stateMachine.ChangeState(deadState))//状态机未初始化或已锁死：回滚死亡标记
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

    public Vector3Int WorldToCell(Vector3 world)//世界坐标转格子坐标
    {
        return grid == null ? Vector3Int.zero : grid.WorldToCell(world);
    }

    public Vector3 CellToCenter(Vector3Int cell)//格子坐标转格中心世界坐标
    {
        return grid == null ? Vector3.zero : grid.GetCellCenterWorld(cell);
    }

    public void SetFacing(Vector2Int direction)//更新四向朝向（零向量忽略）
    {
        if (direction == Vector2Int.zero)
            return;

        facing = direction;

        if (trailEffect != null)//拖尾特效保持在身体背面；上下移动时整体转 90 度
        {
            trailEffect.transform.localPosition = new Vector3(-direction.x * trailOffset, -direction.y * trailOffset, 0f);

            float angle = direction.x != 0 ? 0f : 90f;//左右用横向素材，上下转成纵向
            trailEffect.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer trailSprite = trailEffect.GetComponent<SpriteRenderer>();
            if (trailSprite != null)
                trailSprite.flipX = direction.x != 0 ? direction.x > 0 : direction.y > 0;//让浓的一端朝向身体
        }

        if (spriteRenderer == null)
            return;

        if (direction.x > 0)
            spriteRenderer.flipX = false;
        else if (direction.x < 0)
            spriteRenderer.flipX = true;
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

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && WorldToCell(player.transform.position) == cell)
            return true;

        foreach (Enemy other in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (other == this || other.isDead)
                continue;

            if (other.WorldToCell(other.transform.position) == cell)
                return true;
        }

        return false;
    }

    public bool CanAttack()//冷却是否结束
    {
        return Time.time >= nextAttackTime;
    }

    public void BeginAttackCooldown()//攻击结束后开始冷却
    {
        nextAttackTime = Time.time + attackCooldown;
    }
}
