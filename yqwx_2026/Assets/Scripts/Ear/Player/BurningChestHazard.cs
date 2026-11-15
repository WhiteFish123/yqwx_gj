using UnityEngine;

// 燃烧的箱子会烫人：
//   站在箱子周围 N 格以内，并且朝着箱子的方向按键 → 掉血。
//
// "朝着箱子"的判断：把"玩家→箱子"的向量取主轴方向（上下左右），
//   和当前按着的方向一致才算 —— 所以背对箱子按反方向不会被烫。
public class BurningChestHazard : MonoBehaviour
{
    [Tooltip("危险范围：曼哈顿距离几格以内（1 = 上下左右紧邻的四格）")]
    public int radius = 1;

    [Tooltip("多久重新扫一次场上的箱子（性能用，不用每帧扫）")]
    public float rescanInterval = 0.5f;

    [Tooltip("两次烫伤之间至少隔多久，避免按住不放连续掉血")]
    public float hurtInterval = 1f;

    [Tooltip("每次烫伤掉几点血")]
    public int damagePerBurn = 1;

    private PlayerHealth health;
    private PlayerBrain brain;
    private PlayerGridMove motor;
    private Grid grid;

    private Chest[] chests;
    private float rescanTimer;
    private float hurtTimer;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        brain = GetComponent<PlayerBrain>();
        motor = GetComponent<PlayerGridMove>();
        grid = FindFirstObjectByType<Grid>();
    }

    private void Update()
    {
        if (health == null || brain == null || motor == null || grid == null)
            return;

        if (health.IsDead)
            return;

        //定时重扫箱子（箱子会被点燃/烧完，状态要刷新）
        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = rescanInterval;
            chests = FindObjectsByType<Chest>(FindObjectsSortMode.None);
        }

        if (hurtTimer > 0f)
            hurtTimer -= Time.deltaTime;

        //没按方向键就不算"往火里凑"
        Vector2Int input = brain.moveInput;
        if (input == Vector2Int.zero)
            return;

        Vector2Int playerCell = new Vector2Int(motor.Cell.x, motor.Cell.y);

        foreach (Chest chest in chests)
        {
            if (chest == null || !chest.IsBurning)
                continue;

            Vector2Int toChest = chest.Cell - playerCell;

            if (Mathf.Abs(toChest.x) + Mathf.Abs(toChest.y) > radius)
                continue;//太远

            Vector2Int dirToChest = DominantDirection(toChest);
            if (dirToChest != input)
                continue;//不是朝着箱子按

            if (hurtTimer > 0f)
                continue;

            hurtTimer = hurtInterval;
            health.Hurt(damagePerBurn);
            return;
        }
    }

    //把"玩家→箱子"的向量归一成四方向（取绝对值大的那个轴）
    private static Vector2Int DominantDirection(Vector2Int v)
    {
        if (v == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x > 0 ? Vector2Int.right : Vector2Int.left;

        return v.y > 0 ? Vector2Int.up : Vector2Int.down;
    }
}
