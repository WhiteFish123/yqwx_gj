using UnityEngine;

// 主角的四向待机 / 行走动画。
//
// 为什么不用 Animator：
//   四方向 × 走/停 = 8 组图。用 Animator 要画 8 个状态加一堆过渡，配置量大、手改容易坏；
//   这里直接按"朝向 + 是否在走"换 SpriteRenderer 的图，改几个数字就能调手感。
//   以后真要接 Animator，把本脚本删掉、在 Animator 里重建同样的状态即可，不影响状态机。
//
// 数据来源：每个方向一组"待机帧"和一组"行走帧"，帧数可以不一样（每方向只有 1 张也能用）。
public class PlayerSpriteAnim : MonoBehaviour
{
    [Header("待机帧（每个方向一组，只有 1 张也可以）")]
    public Sprite[] idleDown;
    public Sprite[] idleUp;
    public Sprite[] idleLeft;
    public Sprite[] idleRight;

    [Header("行走帧")]
    public Sprite[] walkDown;
    public Sprite[] walkUp;
    public Sprite[] walkLeft;
    public Sprite[] walkRight;

    [Header("推箱子帧（留空则自动退回行走帧）")]
    public Sprite[] pushDown;
    public Sprite[] pushLeft;
    public Sprite[] pushRight;

    [Header("播放速度（每秒几帧）")]
    public float walkFps = 8f;
    public float idleFps = 3f;
    public float pushFps = 12f;

    private SpriteRenderer sr;
    private PlayerGridMove motor;
    private PlayerBrain brain;

    private Sprite[] currentSet;//当前正在播的那一组
    private int frame;
    private float timer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>(true);//万一以后把图挪到子物体

        motor = GetComponent<PlayerGridMove>();
        brain = GetComponent<PlayerBrain>();

        if (sr == null)
            Debug.LogError("PlayerSpriteAnim：找不到 SpriteRenderer，图放不上去", this);
    }

    private void Update()
    {
        if (sr == null) return;

        Sprite[] set = PickSet();
        if (set == null || set.Length == 0) return;

        //换了一组图（比如从"待机·下"换到"行走·下"）：从第 1 帧重新开始
        if (set != currentSet)
        {
            currentSet = set;
            frame = 0;
            timer = 0f;
            sr.sprite = set[0];
            return;
        }

        if (set.Length <= 1) return;//只有一帧，不用播

        float fps = IsPushing() ? pushFps : (IsWalking() ? walkFps : idleFps);
        if (fps <= 0f) return;

        timer += Time.deltaTime;

        float step = 1f / fps;
        if (timer < step) return;

        timer -= step;
        frame = (frame + 1) % set.Length;
        sr.sprite = set[frame];
    }

    //正在走吗？
    //既看"是否在跨格"，也看"有没有按方向键"：
    //格子移动是一步一步的，两步之间有一瞬间不跨格，只看 IsMoving 会让动画在待机和行走之间抖动。
    private bool IsWalking()
    {
        if (motor != null && motor.IsMoving) return true;
        if (brain != null && brain.moveInput != Vector2Int.zero) return true;
        return false;
    }

    //正在推拉箱子吗？
    private bool IsPushing()
    {
        return motor != null && motor.IsPushing;
    }

    //空数组当作“没配”
    private static Sprite[] NonEmpty(Sprite[] set)
    {
        return (set != null && set.Length > 0) ? set : null;
    }

    //按朝向和状态挑一组图
    private Sprite[] PickSet()
    {
        Vector2Int facing = motor != null ? motor.Facing : Vector2Int.down;
        bool walking = IsWalking();

        //推箱子优先：有该方向的推箱帧就用它，没有就退回行走/待机
        if (IsPushing())
        {
            Sprite[] push = null;
            if (facing == Vector2Int.left) push = NonEmpty(pushLeft);
            else if (facing == Vector2Int.right) push = NonEmpty(pushRight);
            else if (facing != Vector2Int.up) push = NonEmpty(pushDown);

            if (push != null)
                return push;
        }

        if (facing == Vector2Int.up) return walking ? walkUp : idleUp;
        if (facing == Vector2Int.left) return walking ? walkLeft : idleLeft;
        if (facing == Vector2Int.right) return walking ? walkRight : idleRight;

        return walking ? walkDown : idleDown;
    }
}
