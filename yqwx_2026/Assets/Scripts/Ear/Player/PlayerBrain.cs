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
    public KeyCode grabKey = KeyCode.F;//按住抓箱子

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
    }

    private void ReadInput()
    {
        moveInput = ReadMoveInput();

        //只有"手上有能用的武器"时，按 J 才算攻击输入 —— 空手不会进攻击状态
        attackPressed = hand != null && hand.CanUseHeldItem && Input.GetKeyDown(hand.useKey);

        grabHeld = Input.GetKey(grabKey);
    }

    private static Vector2Int ReadMoveInput()//一次读一个方向（优先级：上 下 左 右）
    {
        //★ 用 GetKey（按住持续为真）而不是 GetKeyDown。
        //  原因：GetKeyDown 只在按下那一帧为真，而那一帧的输入会被"Idle→Move"的状态切换吃掉，
        //  MoveState 下一帧再读就已经是 0 了 —— 表现就是"按了方向键但人不动"。
        //  用 GetKey 之后：按住 = 连续走格；轻点一下 = 也至少走一格。
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            return Vector2Int.up;

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            return Vector2Int.down;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            return Vector2Int.left;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            return Vector2Int.right;

        return Vector2Int.zero;
    }

    // ===== 状态切换的公共入口 =====

    //尝试进入攻击状态：空手 / 抓着箱子时都不允许（攻击与交互互斥）
    public bool TryEnterAttack()
    {
        if (!attackPressed || motor.IsGrabbing)
            return false;

        return stateMachine.ChangeState(attackState);
    }

    //尝试进入交互状态：必须按住 F，并且正前方真的抓到箱子
    public bool TryEnterInteract()
    {
        if (!grabHeld || motor.IsMoving)
            return false;

        if (!motor.TryGrab())
            return false;

        return stateMachine.ChangeState(interactState);
    }
}
