// 手持栏：管理"手上拿着哪一件物品"，并负责按 J 使用它。
// ★ 手上存的是一个 InventoryItem（一件具体物品），不是 prefab。
//   因为使用次数存在物品身上；如果传 prefab，每次装备都会新建物品、次数被重置成满，
//   就会变成"丢下再捡起来 = 无限子弹"。
using UnityEngine;

public class HeldItemSlot : MonoBehaviour
{
    [Header("设置")]
    public KeyCode useKey = KeyCode.J;

    [Header("测试用（可留空）")]
    public ItemData startItem;   // 开局手上拿的测试物品

    private PlayerGridMove move;
    private IUsable current;             // 手上物品的"用法"（武器有，血瓶之类没有）
    private GameObject holderInstance;   // 手上物品的实例（由 heldPrefab 例化出来）
    private InventoryItem heldItem;      // ★ 手上这件物品（剩余次数存在这里面）

    public bool HasItem => heldItem != null && holderInstance != null;
    public InventoryItem HeldItem => heldItem;
    public WeaponBase HeldWeapon => current as WeaponBase;

    // 手上这件能不能按 J 使用（武器才有 IUsable；血瓶、钥匙这类是 false）
    // PlayerBrain 用它判断"按 J 算不算攻击输入"，所以空手时不会进攻击状态
    public bool CanUseHeldItem => current != null;

    public string HeldItemName =>
        heldItem != null && heldItem.data != null ? heldItem.data.itemName : "";

    void Awake()
    {
        move = GetComponent<PlayerGridMove>();
    }

    void Start()
    {
        if (startItem != null) Equip(new InventoryItem(startItem));
    }

    void Update()
    {
        // 武器次数用完会自毁；这里发现实例没了，就把物品也清掉（物品被消耗，不回背包）
        if (heldItem != null && holderInstance == null)
        {
            Debug.Log(HeldItemName + " 已耗尽，手上空了");
            heldItem = null;
            current = null;
        }

    }

    // ===== 由状态机（Player_AttackState）调用 =====

    /// <summary>
    /// 使用一次手上的道具，返回这次的后摇时长（0 = 没打出去：冷却中或手上没东西）。
    /// 输入与状态时机现在归 PlayerBrain 管，这里只负责"执行"。
    /// </summary>
    public float UseEquipped()
    {
        if (current == null) return 0f;

        float recovery = current.Use(transform.position, Facing);
        SyncUsesToItem();   // 把武器的剩余次数写回物品
        return recovery;
    }

    //朝向：格子移动的 Facing 是 Vector2Int，这里转成 Vector2 给武器用
    Vector2 Facing => move != null ? new Vector2(move.Facing.x, move.Facing.y) : Vector2.down;

    // 次数以"物品"为准，武器只是临时镜像：每次用完把它读回来写进物品
    void SyncUsesToItem()
    {
        WeaponBase weapon = current as WeaponBase;
        if (weapon != null && heldItem != null)
            heldItem.SetRemainingUses(weapon.RemainingUses);
    }

    // ===== 背包 / 地上的物品只调这两个方法 =====

    /// <summary>装备一件物品。只在手上空着时成功，返回 true = 装上了</summary>
    public bool Equip(InventoryItem item)
    {
        if (item == null || item.data == null || item.data.heldPrefab == null)
            return false;

        // 上一次的物品可能已经自毁，先把残留状态清掉
        if (heldItem != null && holderInstance == null)
        {
            heldItem = null;
            current = null;
        }

        if (HasItem) return false;

        holderInstance = Instantiate(item.data.heldPrefab, transform);
        holderInstance.transform.localPosition = Vector3.zero;

        // 万一 heldPrefab 上带外观或碰撞体，拿在手上时一律关掉（不影响它作为地上物品的形态）
        foreach (Renderer r in holderInstance.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider2D c in holderInstance.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

        heldItem = item;
        current = holderInstance.GetComponent<IUsable>();

        // ★ 把物品身上的次数灌给武器；之后武器每用一次就少一次
        WeaponBase weapon = holderInstance.GetComponent<WeaponBase>();
        if (weapon != null) weapon.ApplyItemUses(item.data.maxUses, item.remainingUses);

        if (current == null)
            Debug.Log("手上物品没有 IUsable（血瓶、钥匙这类属正常）：" + item.data.itemName);

        return true;
    }

    /// <summary>卸下：返回手上这件物品（背包用它收回），手上没有就返回 null</summary>
    public InventoryItem Unequip()
    {
        SyncUsesToItem();   // 先把次数写回物品，再卸下

        InventoryItem result = heldItem;

        if (holderInstance != null) Destroy(holderInstance);
        holderInstance = null;
        current = null;
        heldItem = null;

        return result;
    }
}
