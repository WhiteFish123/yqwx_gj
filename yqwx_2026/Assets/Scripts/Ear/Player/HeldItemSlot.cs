// 手持栏：管理手上拿着的东西，并负责使用它们
using UnityEngine;

public class HeldItemSlot : MonoBehaviour
{
    [Header("设置")]
    public KeyCode useKey = KeyCode.J;

    [Header("测试用")]
    public GameObject startItem;

    private PlayerMove move;
    private IUsable current;            // 手上东西的"用法"
    private GameObject holderInstance;  // 手上东西的实例
    private GameObject heldPrefab;      // 手上东西的 prefab

    // ===== 对外只读状态 =====
    public bool HasItem => heldPrefab != null;        // 手上有没有东西
    public bool CanUseHeldItem => current != null;    // 手上这件能不能按 J 用（非武器为 false）
    public GameObject HeldPrefab => heldPrefab;
    public string HeldItemName => heldPrefab != null ? heldPrefab.name : "";

    void Awake() { move = GetComponent<PlayerMove>(); }

    void Start()
    {
        if (startItem != null) Equip(startItem);   // 测试用：开局手上拿什么
    }

    void Update()
    {
        if (current == null) return;                    // 手上没有能用的东西
        if (!Input.GetKeyDown(useKey)) return;

        float recovery = current.Use(transform.position, Facing);
        if (recovery > 0f && move != null) move.LockMovement(recovery);   // 后摇
    }

    Vector2 Facing => move != null ? move.facing : Vector2.down;

    // ===== 背包调这两个 =====

    // 装备：只在手上空着时成功。
    // 返回 true = 装上了；false = 手上有东西 / 参数为空（此时状态一点都不变）
    public bool Equip(GameObject itemPrefab)
    {
        if (itemPrefab == null) return false;

        if (HasItem)
        {
            Debug.LogWarning("手上已经有 " + HeldItemName + "，要换请先调用 Unequip()", this);
            return false;
        }

        holderInstance = Instantiate(itemPrefab, transform);
        holderInstance.transform.localPosition = Vector3.zero;

        // 拿在手上：隐藏外观 + 关掉碰撞
        //同一个 prefab 在地上时是可见、可拾取的
        foreach (Renderer r in holderInstance.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider2D c in holderInstance.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

        heldPrefab = itemPrefab;
        current = holderInstance.GetComponent<IUsable>();

        if (current == null)
            Debug.Log("手上物品没有 IUsable，无法使用：" + itemPrefab.name);

        return true;
    }

    // 卸下：把手上的东西交出去（背包整理时用）。返回原来拿的是什么，没有就 null
    public GameObject Unequip()
    {
        GameObject result = heldPrefab;

        if (holderInstance != null) Destroy(holderInstance);
        holderInstance = null;
        current = null;
        heldPrefab = null;

        return result;
    }
}