using UnityEngine;

//运行时道具实例：一份数据 + 当前堆叠数 + 剩余使用次数
//★ 这里是"一件具体物品"的唯一身份。物品在"手上/地上/背包"之间转移时，
//   传的都应该是这个对象，而不是 prefab —— 否则使用次数会每次都被重置成满。
public class InventoryItem
{
    public ItemData data { get; private set; }//道具数据
    public int stackSize { get; private set; }//当前堆叠数量
    public int remainingUses { get; private set; }//剩余使用次数；-1 = 无限次

    public InventoryItem(ItemData data, int stackSize = 1)
    {
        this.data = data;
        this.stackSize = Mathf.Max(1, stackSize);
        remainingUses = data != null ? data.maxUses : -1;//初始为满次数
    }

    //写回剩余使用次数（超出范围会被夹紧；无限次物品永远是 -1）
    public void SetRemainingUses(int value)
    {
        if (data == null || data.maxUses < 0)
        {
            remainingUses = -1;
            return;
        }

        remainingUses = Mathf.Clamp(value, 0, data.maxUses);
    }

    public bool CanAddStack()//是否还能堆叠（非消耗品永不堆叠）
    {
        return data != null && data.isConsumable && stackSize < data.maxStack;
    }

    public void AddStack()//堆叠一个
    {
        stackSize++;
    }

    public bool RemoveStack()//减少一个，返回是否已空
    {
        stackSize = Mathf.Max(0, stackSize - 1);
        return stackSize <= 0;
    }
}
