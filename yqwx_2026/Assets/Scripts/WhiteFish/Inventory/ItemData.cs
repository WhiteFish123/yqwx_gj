using UnityEngine;

// 从背包里点"使用"时触发什么效果。
public enum ItemEffect
{
    None,   // 没效果（武器、油、钥匙……）
    Heal    // 回血（数量见 ItemData.effectAmount）
}

//道具数据：在 Assets/Data/Items 下创建资产
[CreateAssetMenu(menuName = "WhiteFish/Item Data", fileName = "Item - ")]
public class ItemData : ScriptableObject
{
    public string itemName = "道具";//显示名称
    [TextArea] public string description = "描述";//浮窗里的说明
    public Sprite icon;//图标
    [Min(1)] public int maxStack = 99;//最大堆叠数
    [Tooltip("取消勾选表示非消耗品：不会与背包里已有的同类合并，每次都是一个独立格子")]
    public bool isConsumable = true;//是否消耗品（可堆叠）

    [Header("武器与外观")]
    [Tooltip("这件物品在地上的 prefab：要有 SpriteRenderer + Trigger 碰撞体 + Pickup 组件")]
    public GameObject worldPrefab;

    [Tooltip("这件物品拿在手上时的 prefab：只需要挂武器脚本，不需要外观和碰撞体")]
    public GameObject heldPrefab;

    [Tooltip("可使用次数；-1 表示无限次（例如刀）。武器装备时读这个值，用完自毁")]
    public int maxUses = -1;//使用次数，-1 = 无限

    [Tooltip("勾上＝这件武器的弹药没有上限：捡到的子弹可以一直往上加，打空了也不会自毁（枪用它）")]
    public bool ammoNoLimit;

    [Header("使用效果（在背包里点\"使用\"时生效）")]
    [Tooltip("拿在手上按 J 用的效果由 heldPrefab 上的脚本决定，不走这里")]
    public ItemEffect useEffect = ItemEffect.None;

    [Tooltip("效果数值：Heal 表示回几点血")]
    public int effectAmount = 1;
}
