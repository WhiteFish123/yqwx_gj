using UnityEngine;

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
}
