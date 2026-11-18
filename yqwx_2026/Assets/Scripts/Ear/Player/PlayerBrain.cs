using UnityEngine;

// 主角大脑：持有状态机与四个状态，并每帧读一次输入给所有状态共用。
// 结构与学长的 Enemy 完全一致（StateMachine + EntityState 子类），两个人互相看得懂。
//
// 为什么输入要统一在这里读：
//   Input.GetKeyDown 只在"按下的那一帧"为 true。
//   如果每个状态自己去读，Idle 读到了、下一帧 MoveState 再读就变成 false 了，
//   会出现"按一下方向键，状态切了但人没走"的怪 bug。
public class PlayerBrain : MonoBehaviour
{
    [Header("输入")]
    public KeyCode grabKey = KeyCode.LeftShift;//按住抓箱子（左 Shift；右手小指够得着，不和 WASD 抢手）

    //由外部（传送、过场等）临时关掉；关掉期间所有输入都当作没有
    public bool inputEnabled = true;

    public Animator anim { get; private set; }//动画（可以为空：没有 Animator 时只走逻辑）
    public PlayerGridMove motor { get; private set; }//格子移动引擎
    public HeldItemSlot hand { get; private set; }//手持栏
    public StateMachine stateMachine { get; private set; }//状态机

    public Player_IdleState idleState;
    public Player_MoveState moveState;
    public Player_AttackState attackState;
    public Player_InteractState interactState;

    //每帧刷新的输入快照（所有状态共用）
    public Vector2Int moveInput { get; private set; }
    public bool attackPressed { get; private set; }
    public bool grabHeld { get; private set; }

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        motor = GetComponent<PlayerGridMove>();
        hand = GetComponent<HeldItemSlot>();

        //motor 是必需的：没有它人物一步都走不了
        if (motor == null)
            Debug.LogError("PlayerBrain 需要和 PlayerGridMove 挂在同一个物体上，否则人物无法移动", this);

        //hand 可选：没有手持栏的场景只能走动，不能攻击
        if (hand == null)
            Debug.LogWarning("这个物体上没有 HeldItemSlot：本场景里玩家不能使用武器", this);

        stateMachine = new StateMachine();

        //和 Enemy_Slime 一样的写法：状态在这里创建，动画 bool 名字一起传进去
        idleState = new Player_IdleState(this, stateMachine, "Idle");
        moveState = new Player_MoveState(this, stateMachine, "Move");
        attackState = new Player_AttackState(this, stateMachine, "Attack");
        interactState = new Player_InteractState(this, stateMachine, "Interact");
    }

    private void Start()
    {
        stateMachine.Initialize(idleState);
    }

    private void Update()
    {
        ReadInput();
        stateMachine.UpdateActiveState();
        UpdateAnimatorParameters();
    }

    private float movingGrace;//跨格之间的短暂空档，避免"走/停"在两帧之间来回抖

    //把状态同步给 Animator：朝向 Dir，以及推拉时补上 Move/Idle 并按移动速度缩放速率
    //没有 Animator（或还没指定 controller）时自动跳过，不会报错
    private void UpdateAnimatorParameters()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;

        bool grabbing = motor != null && motor.IsGrabbing;

        //① 推拉时手动驱动"走/停"：
        //   Interact 状态只设 Interact 这个 bool，不会设 Move/Idle，
        //   所以推着箱子走的时候要在这里补 —— 否则人物会一直停在待机图上。
        if (grabbing)
        {
            //一整格刚走完时会有一帧是"没在跨格"，给 0.08 秒宽限，避免动画闪回待机
            if (motor.IsMoving) movingGrace = 0.08f;
            else movingGrace -= Time.deltaTime;

            bool moving = motor.IsMoving || movingGrace > 0f;

            anim.SetBool("Move", moving);
            anim.SetBool("Idle", !moving);

            //推拉比走路慢，动画速率按比例降低（0.5 = 慢一倍）
            anim.speed = motor.PushPullSpeedScale;
        }
        else
        {
            movingGrace = 0f;
            anim.speed = 1f;
        }

        //② 朝向 = 实际移动方向（motor.Facing）。
        //   推拉时箱子恒在 Facing 那一格，所以这条规则天然满足"永远面对箱子"：
        //   往前推是面朝箱子走，往后拉是面朝箱子退。
        Vector2Int facing = motor != null ? motor.Facing : Vector2Int.down;

        int dir = 0;
        if (facing == Vector2Int.up) dir = 1;
        else if (facing == Vector2Int.left) dir = 2;
        else if (facing == Vector2Int.right) dir = 3;

        anim.SetInteger("Dir", dir);
    }

    private void ReadInput()
    {
        //被外部锁住（例如传送淡出中）：一律当作没有任何输入
        if (!inputEnabled)
        {
            moveInput = Vector2Int.zero;
            attackPressed = false;
            grabHeld = false;
            return;
        }

        moveInput = ReadMoveInput();

        //只有"手上有能用的武器"时，按 J 才算攻击输入 —— 空手不会进攻击状态
        bool attackKeyDown = hand != null && Input.GetKeyDown(hand.useKey);

        //手上有东西、但按 J 完全没效果（油、钥匙这类）：提示一句，别让玩家以为卡住了
        if (attackKeyDown && hand != null && hand.HeldItem != null && !hand.CanUseHeldItem)
            ScreenMessage.Show("把这东西拿在手上能干什么？");

        attackPressed = attackKeyDown && hand != null && hand.CanUseHeldItem;

        grabHeld = Input.GetKey(grabKey);
    }

    private static Vector2Int lastPressedDir = Vector2Int.zero;//最后按下的方向

    private static Vector2Int ReadMoveInput()
    {
        //★ 读方向用的是 GetKey（按住持续为真），不是 GetKeyDown。
        //  原因：GetKeyDown 只在按下那一帧为真，而那一帧的输入会被"Idle→Move"的状态切换吃掉，
        //  MoveState 下一帧再读就已经是 0 了 —— 表现就是"按了方向键但人不动"。

        //① 记录"最后按下"的方向：同时按住两个键时，转向后按的那一个（玩家直觉）
        if (PressDown(KeyCode.W, KeyCode.UpArrow)) lastPressedDir = Vector2Int.up;
        else if (PressDown(KeyCode.S, KeyCode.DownArrow)) lastPressedDir = Vector2Int.down;
        else if (PressDown(KeyCode.A, KeyCode.LeftArrow)) lastPressedDir = Vector2Int.left;
        else if (PressDown(KeyCode.D, KeyCode.RightArrow)) lastPressedDir = Vector2Int.right;

        //② 最后按下的还按着，就用它
        if (IsHeld(lastPressedDir))
            return lastPressedDir;

        //③ 松开了，退回"还按着的任意一个方向"（兜底顺序：上 下 左 右）
        if (IsHeld(Vector2Int.up)) return Vector2Int.up;
        if (IsHeld(Vector2Int.down)) return Vector2Int.down;
        if (IsHeld(Vector2Int.left)) return Vector2Int.left;
        if (IsHeld(Vector2Int.right)) return Vector2Int.right;

        return Vector2Int.zero;
    }

    private static bool PressDown(KeyCode a, KeyCode b)
    {
        return Input.GetKeyDown(a) || Input.GetKeyDown(b);
    }

    private static bool IsHeld(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        if (dir == Vector2Int.down) return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        if (dir == Vector2Int.left) return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        if (dir == Vector2Int.right) return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        return false;
    }

    // ===== 状态切换的公共入口 =====

    //尝试进入攻击状态：空手 / 抓着箱子时都不允许（攻击与交互互斥）
    public bool TryEnterAttack()
    {
        if (!attackPressed || motor.IsGrabbing)
            return false;

        return stateMachine.ChangeState(attackState);
    }

    //尝试进入交互状态：必须按住左 Shift，并且正前方真的抓到箱子
    public bool TryEnterInteract()
    {
        if (!grabHeld || motor.IsMoving)
            return false;

        if (!motor.TryGrab())
            return false;

        return stateMachine.ChangeState(interactState);
    }
}
