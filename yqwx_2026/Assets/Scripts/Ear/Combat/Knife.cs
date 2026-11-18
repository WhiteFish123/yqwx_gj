using UnityEngine;

// 刀：近战武器，在玩家正前方扫一块矩形区域，区域内所有可受击对象都吃伤害。
// 使用次数、冷却、后摇都由 WeaponBase 统一管理（刀在物品数据 ItemData 里配成 -1 = 无限次）。
public class Knife : WeaponBase
{
    [Header("刀")]
    public float damage = 1f;    // 命中伤害
    public float reach = 2f;     // 往前够多远（矩形的长边）
    public float width = 2f;     // 判定框有多宽（矩形的短边）

    [Header("刀光特效")]
    [Tooltip("刀光 prefab，留空则不放特效")]
    public GameObject slashPrefab;

    [Tooltip("刀光离角色多远（默认和判定框中心重合 = reach 的一半）")]
    public float slashDistance = 0.75f;

    [Tooltip("刀光生成后多久销毁（要略大于动画时长）")]
    public float slashLifeTime = 0.35f;

    [Header("刀光朝向")]
    [Tooltip("朝右挥刀时刀光的旋转角（度）。贴图本身歪的话就在这儿补：顺时针歪就填正数")]
    public float slashBaseAngle = 0f;

    [Tooltip("朝上时在 Base Angle 上加多少度")]
    public float slashUpAngle = 90f;

    [Tooltip("朝下时在 Base Angle 上加多少度")]
    public float slashDownAngle = -90f;

    [Tooltip("朝左时用左右镜像（推荐）。关掉就退化成旋转 180° —— 弧线动画会看起来是反的")]
    public bool mirrorWhenFacingLeft = true;

    [Header("刀光位置微调")]
    [Tooltip("刀光在\"它自己的坐标系\"里的偏移（会跟着刀光一起旋转）。" +
             "左右挥刀太靠下 → Y 填正数（例如 0.2）；左右有前后偏差 → 调 X")]
    public Vector2 slashLocalOffset = Vector2.zero;

    [Tooltip("刀光整体缩放。刀光比判定框大就调小（0.7 左右），比判定框小就调大")]
    public float slashScale = 1f;

    [Tooltip("刀光动画播放速率（乘在 Animator.speed 上）。1 = 按 clip 原速；2 = 快一倍")]
    public float slashAnimSpeed = 1f;

    [Header("调试")]
    [Tooltip("勾上后挥刀时在 Scene 视图里画出红色判定框（Play 时看得到，不进游戏画面）")]
    public bool drawHitBox = false;

    protected override void DoUse(Vector2 origin, Vector2 facing)
    {
        // 判定框的中心：从使用者中心往朝向前方推半个 reach
        Vector2 center = origin + facing * (reach * 0.5f);

        // 朝左右时是"横条"，朝上下时是"竖条" —— 长边永远顺着朝向
        Vector2 size = facing.x != 0f
            ? new Vector2(reach, width)
            : new Vector2(width, reach);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);

        if (drawHitBox)
            DrawHitBox(center, size);

        int count = 0;
        foreach (Collider2D hit in hits)
        {
            //★ 别砍到自己：手里的刀是玩家的子物体，所以两者的 root 是同一个。
            //  （玩家现在实现了 IDamageable，不跳过的话一刀会砍自己一滴血）
            if (hit.transform.root == transform.root) continue;

            // 只有实现 IDamageable 的物体才算命中；墙、地板、地上的物品都会被跳过
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target == null) continue;

            HitInfo info = new HitInfo();
            info.damage = damage;
            info.type = HitType.knife;
            target.TakeHit(info);
            count++;
        }

        SpawnSlash(origin, facing);

        //挥刀音效（刀有冷却，不会叠音）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx("sfx_slash");

        Debug.Log("挥刀：命中 " + count + " 个目标");
    }

    //调试用：在 Scene 视图里把判定框画成红色方框（持续 0.4 秒）
    private void DrawHitBox(Vector2 center, Vector2 size)
    {
        Vector2 half = size * 0.5f;

        Vector3 a = new Vector3(center.x - half.x, center.y - half.y, 0f);
        Vector3 b = new Vector3(center.x + half.x, center.y - half.y, 0f);
        Vector3 c = new Vector3(center.x + half.x, center.y + half.y, 0f);
        Vector3 d = new Vector3(center.x - half.x, center.y + half.y, 0f);

        Debug.DrawLine(a, b, Color.red, 0.4f);
        Debug.DrawLine(b, c, Color.red, 0.4f);
        Debug.DrawLine(c, d, Color.red, 0.4f);
        Debug.DrawLine(d, a, Color.red, 0.4f);
    }

    //在角色前方放一段刀光。
    //位置用和判定框同一个中心点。
    //
    //★ 左右方向用"镜像"而不是"旋转 180°"：
    //  刀光是弧线，旋转 180° 后弧的弯向会反掉（看起来像是反着挥）。
    //  镜像（flipX）才是玩家期望的"左手挥刀"。
    private void SpawnSlash(Vector2 origin, Vector2 facing)
    {
        if (slashPrefab == null)
            return;

        float angle;
        bool mirror = false;

        if (facing.x != 0f)
        {
            //左右：横向刀光，不额外旋转
            angle = slashBaseAngle;
            mirror = mirrorWhenFacingLeft && facing.x < 0f;
        }
        else
        {
            //上下：在基准角上再加 90 / -90
            angle = slashBaseAngle + (facing.y > 0f ? slashUpAngle : slashDownAngle);
        }

        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        //偏移量要跟着刀光一起转：这样"左右太靠下"这个错位
        //在朝上时会自动变成"太靠右"、朝下时变成"太靠左"（你在游戏里看到的现象），
        //只要把 slashLocalOffset.y 调正，四个方向就一起修好了。
        Vector3 localOffset = mirror ? new Vector3(-slashLocalOffset.x, slashLocalOffset.y, 0f)
                                     : new Vector3(slashLocalOffset.x, slashLocalOffset.y, 0f);

        //Vector2 和 Vector3 不能直接相加（会报 CS0034 歧义），先转成 Vector3
        Vector3 position = (Vector3)(origin + facing * slashDistance) + rotation * localOffset;
        GameObject fx = Instantiate(slashPrefab, position, rotation);

        //整体缩放：让刀光的可见范围贴合判定框（等比，避免把弧形拉歪）
        fx.transform.localScale = Vector3.one * slashScale;

        //播放速率：不用改 clip 的 Samples，也能在 Inspector 里直接调快慢
        Animator fxAnimator = fx.GetComponentInChildren<Animator>(true);
        if (fxAnimator != null)
            fxAnimator.speed = slashAnimSpeed;

        if (mirror)
        {
            //优先用 SpriteRenderer.flipX（干净，不动 scale）；
            //没有渲染器（比如特效是纯粒子）时退回负缩放
            SpriteRenderer renderer = fx.GetComponentInChildren<SpriteRenderer>(true);

            if (renderer != null)
            {
                renderer.flipX = !renderer.flipX;
            }
            else
            {
                Vector3 scale = fx.transform.localScale;
                scale.x = -scale.x;
                fx.transform.localScale = scale;
            }
        }

        Destroy(fx, slashLifeTime);//播完就删，免得越堆越多
    }
}
