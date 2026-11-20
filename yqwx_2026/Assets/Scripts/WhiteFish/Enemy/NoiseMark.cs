using System.Collections;
using UnityEngine;

//头顶表情：听到噪音时冒问号，发现玩家时冒感叹号
//两种都是「一组 Sprite 逐帧播放一次，停一下再消失，不循环」
//问号由 Enemy.HearNoise 调 NoiseMark.Show(enemy) 触发；
//感叹号由这里的 Update 监听状态机：一进入追逐/蓄力状态就播一次
//不用 Animator，方便直接在 Inspector 里拖换帧、改每帧时长
public class NoiseMark : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;//留空则用自身身上的渲染器
    [SerializeField] private Sprite[] frames;//按顺序播放的问号帧（听到声音）
    [SerializeField] private Sprite[] spottedFrames;//按顺序播放的感叹号帧（发现玩家）
    [SerializeField] private float frameDuration = 0.09f;//每帧播放多久
    [SerializeField] private float holdTime = 1f;//最后一次播放的最后一帧停留多久再消失
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.05f, 0f);//相对「身体右上角」的额外偏移

    private Coroutine routine;
    private Enemy owner;
    private EntityState lastState;//上一帧的状态，用来抓「刚进入追击」的那一瞬间

    private void Awake()
    {
        owner = GetComponentInParent<Enemy>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = false;
        PlaceAtTopRight();
    }

    //监视状态机：从非追击状态切到「察觉玩家的状态」时弹一次感叹号
    private void Update()
    {
        if (owner == null || owner.stateMachine == null)
            return;

        EntityState current = owner.stateMachine.currentState;

        if (current == lastState)
            return;

        lastState = current;

        if (current != null && current == owner.GetAlertState())
            PlaySpotted();
    }

    private void OnDisable()//怪物死亡/被回收时立刻收起来，避免留下一个悬空的符号
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    //按怪物身体贴图的真实大小把自己摆到右上角，换怪物素材也不用改数值
    private void PlaceAtTopRight()
    {
        Enemy body = owner != null ? owner : GetComponentInParent<Enemy>();

        if (body == null)
            return;

        SpriteRenderer bodyRenderer = BodyRendererOf(body);

        if (bodyRenderer == null)
            return;

        Bounds bounds = bodyRenderer.bounds;

        Vector2 half = spriteRenderer != null && spriteRenderer.sprite != null
            ? (Vector2)spriteRenderer.sprite.bounds.extents
            : new Vector2(0.25f, 0.25f);

        transform.position = new Vector3(
            bounds.max.x + half.x * 0.4f + offset.x,
            bounds.max.y + half.y * 0.9f + offset.y,
            transform.position.z);
    }

    //取怪物身体的渲染器：运行时用 Enemy 缓存的那个；编辑器里（没 Awake）退回找 Animation 子物体
    private SpriteRenderer BodyRendererOf(Enemy body)
    {
        if (body.spriteRenderer != null && body.spriteRenderer != spriteRenderer)
            return body.spriteRenderer;

        Transform animation = body.transform.Find("Animation");

        if (animation != null)
        {
            SpriteRenderer found = animation.GetComponent<SpriteRenderer>();

            if (found != null)
                return found;
        }

        SpriteRenderer[] all = body.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != spriteRenderer)
                return all[i];
        }

        return null;
    }

#if UNITY_EDITOR
    //非运行时改 Offset 也能立刻看到符号会出现在哪（配合下面的 Gizmos）
    private void OnValidate()
    {
        if (Application.isPlaying || spriteRenderer == null)
            return;

        PlaceAtTopRight();
    }

    //在 Scene 视图里画一个黄框，标出符号实际出现的位置，不用运行游戏就能对齐
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            PlaceAtTopRight();//每次重画都按当前 Offset 重新算，调数值时黄框跟着动

        float size = spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.sprite.bounds.size.x
            : 0.5f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(size, size, 0f));
    }
#endif

    public void Play()//听到声音：问号
    {
        PlaySet(frames);
    }

    public void PlaySpotted()//发现玩家：感叹号
    {
        PlaySet(spottedFrames);
    }

    private void PlaySet(Sprite[] set)
    {
        if (spriteRenderer == null || set == null || set.Length == 0)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        //每次播放前重新摆一次：Awake 时怪物的 Enemy 组件可能还没初始化，取不到身体贴图
        PlaceAtTopRight();

        //只播一遍：已经播完才允许下一次播放，不循环也不重播
        if (routine != null)
            return;

        routine = StartCoroutine(PlayRoutine(set));
    }

    private IEnumerator PlayRoutine(Sprite[] set)
    {
        spriteRenderer.enabled = true;

        for (int i = 0; i < set.Length; i++)
        {
            if (set[i] != null)
                spriteRenderer.sprite = set[i];

            yield return new WaitForSeconds(frameDuration);
        }

        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        spriteRenderer.enabled = false;
        routine = null;
    }

    //给 Enemy 用的快捷入口：找到这只怪物身上（含未激活子物体）的标记并播放问号
    public static void Show(Enemy enemy)
    {
        if (enemy == null)
            return;

        NoiseMark mark = enemy.GetComponentInChildren<NoiseMark>(true);

        if (mark != null)
            mark.Play();
    }

    //发现玩家时冒感叹号
    public static void ShowSpotted(Enemy enemy)
    {
        if (enemy == null)
            return;

        NoiseMark mark = enemy.GetComponentInChildren<NoiseMark>(true);

        if (mark != null)
            mark.PlaySpotted();
    }
}
