using UnityEngine;

// 普通门：关着时挡路，走近按 E 开关。
//
// ★ 挡路的原理（重要，别改成删碰撞体）：
//   玩家走格子前会查"目标格子里有没有 Obstacle 层的碰撞体"（PlayerGridMove.blockingMask），
//   敌人寻路也一样（GridPathfinder 的 obstacleMask）。
//   所以开门不是把碰撞体关掉，而是把整扇门换到 Default 层：
//     - 换层后不再挡玩家，也不再挡敌人寻路
//     - 但物体和碰撞体都还在，所以"按 E"的交互检测照样能扫到它 —— 才能再关回去
//   （如果把碰撞体 enabled = false，门就再也点不到了，只能开不能关）
public class Door : MonoBehaviour, IInteractable
{
    [Header("状态")]
    public bool isOpen;//勾上 = 一开始就是开着的

    [Header("外观")]
    [Tooltip("关着时的图。留空则用物体原本的 Sprite")]
    public Sprite closedSprite;

    [Tooltip("开着时的图。留空则用\"变半透明\"表示已开")]
    public Sprite openSprite;

    [Range(0f, 1f)]
    [Tooltip("没有 openSprite 时，开门后的透明度")]
    public float openAlpha = 0.35f;

    [Header("阻挡")]
    [Tooltip("关着时所在的层。必须和 PlayerGridMove 的 Blocking Mask、敌人的 Obstacle Mask 一致")]
    public string blockingLayer = "Obstacle";

    [Header("音效（可选）")]
    public AudioClip openSound;
    public AudioClip closeSound;

    private SpriteRenderer spriteRenderer;
    private int blockingLayerIndex;
    private int openLayerIndex;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        CacheLayers();
        Apply();
    }

    //把层名解析成索引；解析失败就保持当前层，并提示
    private void CacheLayers()
    {
        blockingLayerIndex = LayerMask.NameToLayer(blockingLayer);

        if (blockingLayerIndex < 0)
        {
            Debug.LogWarning("Door：找不到层 \"" + blockingLayer + "\"，改用物体当前层，门可能不会挡路", this);
            blockingLayerIndex = gameObject.layer;
        }

        openLayerIndex = LayerMask.NameToLayer("Default");
        if (openLayerIndex < 0) openLayerIndex = 0;
    }

    // ===== IInteractable =====

    public string GetPrompt()
    {
        return isOpen ? "关门" : "开门";
    }

    public void Interact(GameObject interactor)
    {
        Toggle();
    }

    // ===== 开关 =====

    public void Toggle()
    {
        isOpen = !isOpen;
        Apply();

        PlaySound(isOpen ? openSound : closeSound);
    }

    //把"开着/关着"同步到层和外观
    private void Apply()
    {
        //1) 换层：关着 = 挡路层，开着 = Default
        gameObject.layer = isOpen ? openLayerIndex : blockingLayerIndex;

        //2) 换外观
        if (spriteRenderer == null)
            return;

        if (!isOpen && closedSprite != null)
            spriteRenderer.sprite = closedSprite;

        if (isOpen && openSprite != null)
        {
            spriteRenderer.sprite = openSprite;

            Color opened = spriteRenderer.color;
            opened.a = 1f;
            spriteRenderer.color = opened;
            return;
        }

        //没有专门的开/关图：开着时变半透明
        Color color = spriteRenderer.color;
        color.a = isOpen ? openAlpha : 1f;
        spriteRenderer.color = color;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    //编辑器里勾/取消 isOpen 就能直接看到效果，方便摆关卡
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        CacheLayers();
        Apply();
    }
}
