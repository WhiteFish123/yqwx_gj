using UnityEngine;

//运行时道具实例：一份数据 + 当前堆叠数
public class InventoryItem
{
    public ItemData data { get; private set; }//道具数据
    public int stackSize { get; private set; }//当前堆叠数量

    public InventoryItem(ItemData data, int stackSize = 1)
    {
        this.data = data;
        this.stackSize = Mathf.Max(1, stackSize);
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
