// 散落在地上的物品：玩家走近按 E（交互键）就捡起来。
//
// 规则：
//   ① 手上空着        → 装到手上
//   ② 手上有东西      → 放进背包
//   ③ 手上和背包都满  → 提示"没有多余的空间"，按 E 不做任何事
//
// 提示文字会带上剩余使用次数（例：拾取手枪（4/6）），无限次物品（刀）不显示括号。
using UnityEngine;

public class Pickup : MonoBehaviour, IInteractable
{
    [Header("这是哪件物品")]
    public ItemData itemData;

    [Tooltip("这件物品还剩几次使用；-1 表示按满次数算（全新物品留 -1 即可）")]
    public int remainingUses = -1;

    [Header("捡起来之后播的对话")]
    [Tooltip("留空就不播；填 Yarn 节点名，例：L1_Knife_Pickup / L2_Lighter")]
    public string dialogueOnPickup = "";

    [Header("子弹模式（勾上就不是道具，而是给枪补弹）")]
    [Tooltip("勾上：这个拾取物是地上的子弹，捡起来直接加给对应的枪，不进背包")]
    public bool isAmmo;

    [Tooltip("这批子弹补给哪把枪（填 Item - Gun）")]
    public ItemData ammoFor;

    [Tooltip("这一次拾取加多少发（可每堆单独调）")]
    public int ammoAmount = 8;

    private Inventory bag;//缓存背包，避免每帧 FindFirstObjectByType
    private HeldItemSlot slot;//缓存手持栏

    // 场上所有还没被捡走的地面道具：玩家不能走进它们所在的格子
    private static readonly System.Collections.Generic.List<Pickup> active =
        new System.Collections.Generic.List<Pickup>();

    private void OnEnable()
    {
        if (!active.Contains(this))
            active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    // 这一格有没有待拾取的道具（有的话玩家走不进去）
    public static bool BlocksCell(Grid grid, Vector3Int cell)
    {
        if (grid == null)
            return false;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Pickup pickup = active[i];

            if (pickup == null)
            {
                active.RemoveAt(i);
                continue;
            }

            if (grid.WorldToCell(pickup.transform.position) == cell)
                return true;
        }

        return false;
    }

    private void Start()
    {
        EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (bag == null) bag = FindFirstObjectByType<Inventory>();
        if (slot == null) slot = FindFirstObjectByType<HeldItemSlot>();
    }

    //还有没有地方放：手上空着，或者背包有空格
    private bool HasRoom()
    {
        EnsureRefs();

        if (slot != null && !slot.HasItem)
            return true;

        return bag != null && bag.HasEmptySlot();
    }

    //现在能不能捡（手上有空位，或者背包有空位；子弹看有没有带枪）
    public bool CanPickUp
    {
        get
        {
            if (isAmmo)
                return FindAmmoOwner() != null;

            return itemData != null && HasRoom();
        }
    }

    public string GetPrompt()
    {
        if (isAmmo)
        {
            if (FindAmmoOwner() == null)
                return "需要先找到一把枪";

            return "拾取子弹 ×" + Mathf.Max(1, ammoAmount);
        }

        if (itemData == null)
            return "拾取物品";

        //没地方放：直接把原因写在提示里，按 E 也不会有效果
        if (!HasRoom())
            return "你没有多余的空间容纳" + itemData.itemName + "了";

        //无限次物品（刀）不显示次数
        //无上限武器（枪）弹药随时会变，也不显示「0/0」这种没意义的东西
        if (itemData.maxUses < 0 || itemData.ammoNoLimit)
            return "拾取" + itemData.itemName;

        int uses = remainingUses >= 0 ? remainingUses : itemData.maxUses;
        return "拾取" + itemData.itemName + "（" + uses + "/" + itemData.maxUses + "）";
    }

    public void Interact(GameObject interactor)
    {
        //子弹：直接加给对应的枪，不进背包
        if (isAmmo)
        {
            if (AddAmmo())
            {
                PlayPickupDialogue(Mathf.Max(1, ammoAmount));
                Destroy(gameObject);
            }

            return;
        }

        if (itemData == null)
        {
            Debug.LogWarning("Pickup 没有设置 itemData", this);
            return;
        }

        HeldItemSlot hand = interactor.GetComponent<HeldItemSlot>();
        if (hand == null) return;

        //放不下就什么都不做（提示文字已经说明原因）
        if (!HasRoom()) return;

        //先给这件物品造一个"运行时实例"，把地上的剩余次数带过去
        InventoryItem item = new InventoryItem(itemData);
        if (remainingUses >= 0) item.SetRemainingUses(remainingUses);

        //① 手上空 → 进手上
        if (hand.Equip(item))
        {
            PlayPickupDialogue();
            Destroy(gameObject);
            return;
        }

        //② 手上有 → 进背包（用 AddExistingItem 才会保留剩余次数）
        if (bag != null && bag.AddExistingItem(item))
        {
            PlayPickupDialogue();
            Destroy(gameObject);
            return;
        }

        //③ 理论上走不到这里（HasRoom 已经拦过），保险起见留在原地
        Debug.Log("手上和背包都放不下：" + itemData.itemName);
    }

    //找一把能装这批子弹的枪：优先手上那把，其次背包里的
    private InventoryItem FindAmmoOwner()
    {
        EnsureRefs();

        if (ammoFor == null)
            return null;

        if (slot != null && slot.HeldItem != null && slot.HeldItem.data == ammoFor)
            return slot.HeldItem;

        if (bag != null)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                InventoryItem item = bag.GetItem(i);
                if (item != null && item.data == ammoFor)
                    return item;
            }
        }

        return null;
    }

    //把子弹加上去；加成功返回 true
    private bool AddAmmo()
    {
        EnsureRefs();

        int amount = Mathf.Max(1, ammoAmount);
        InventoryItem owner = FindAmmoOwner();

        if (owner == null)
            return false;

        //① 枪正在手上：直接加到武器的剩余次数上，再写回物品
        if (slot != null && slot.HeldItem == owner && slot.HeldWeapon != null)
        {
            slot.HeldWeapon.AddAmmo(amount);
            slot.SyncUsesToItem();
            return true;
        }

        //② 枪在背包里：加到那件物品的剩余次数上
        owner.AddUses(amount);

        if (bag != null)
            bag.NotifyChanged();

        return true;
    }

    //捡起来之后播一段对话（刀、打火机这种需要解说的物品用）
    //ammoAdded >= 0 时先把这个数字写进 Yarn 变量 $ammo_added，供"装上 X 发子弹"这种台词用
    private void PlayPickupDialogue(int ammoAdded = -1)
    {
        if (string.IsNullOrEmpty(dialogueOnPickup))
            return;

        Yarn.Unity.DialogueRunner runner = FindFirstObjectByType<Yarn.Unity.DialogueRunner>();

        if (runner == null || runner.IsDialogueRunning)
            return;

        if (ammoAdded >= 0 && runner.VariableStorage != null)
            runner.VariableStorage.SetValue("$ammo_added", (float)ammoAdded);

        //优先走 DialogueHotkey：顺手锁住玩家输入并接管推进/选项按键
        DialogueHotkey hotkey = FindFirstObjectByType<DialogueHotkey>();

        if (hotkey != null)
            hotkey.BeginDialogue(dialogueOnPickup);
        else
            runner.StartDialogue(dialogueOnPickup);
    }

    // ===== 给"背包丢弃"用的静态方法 =====    /// <summary>把一件物品放回地面，并让它记住自己的剩余次数</summary>
    public static Pickup Spawn(ItemData data, int uses, Vector3 position)
    {
        if (data == null || data.worldPrefab == null)
        {
            Debug.LogWarning("Pickup.Spawn 失败：itemData 或 worldPrefab 为空");
            return null;
        }

        GameObject go = Instantiate(data.worldPrefab, position, Quaternion.identity);

        Pickup pickup = go.GetComponent<Pickup>();
        if (pickup == null)
        {
            Debug.LogWarning("地上的 prefab 上没有挂 Pickup 组件：" + data.worldPrefab.name);
            return null;
        }

        pickup.itemData = data;
        pickup.remainingUses = uses;
        return pickup;
    }
}
