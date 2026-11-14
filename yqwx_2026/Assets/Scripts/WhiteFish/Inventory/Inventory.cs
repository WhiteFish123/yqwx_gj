using System;
using System.Collections.Generic;
using UnityEngine;

//背包容器：固定 20 格，同类道具自动堆叠
public class Inventory : MonoBehaviour
{
    public const int SlotCount = 20;//格子总数

    [Header("初始道具（仅用于测试）")]
    [SerializeField] private List<ItemData> startingItems = new List<ItemData>();//开局自动放入的道具
    [SerializeField] private List<int> startingCounts = new List<int>();//对应的初始数量

    private InventoryItem[] slots = new InventoryItem[SlotCount];//格子内容（null 表示空）

    public event Action OnChanged;//内容变化通知

    public InventoryItem GetItem(int index)//读取某格内容
    {
        if (index < 0 || index >= SlotCount)
            return null;

        return slots[index];
    }

    private void Start()
    {
        GiveStartingItems();
    }

    public bool AddItem(ItemData data, int count = 1)//加入道具，自动堆叠；放不下返回 false
    {
        if (data == null || count <= 0)
            return false;

        int remaining = count;

        for (int i = 0; i < SlotCount && remaining > 0; i++)//先堆叠到已有同类格子
        {
            InventoryItem item = slots[i];
            if (item == null || item.data != data)
                continue;

            while (remaining > 0 && item.CanAddStack())
            {
                item.AddStack();
                remaining--;
            }
        }

        for (int i = 0; i < SlotCount && remaining > 0; i++)//再占用空位
        {
            if (slots[i] != null)
                continue;

            int amount = Mathf.Min(remaining, data.maxStack);
            slots[i] = new InventoryItem(data, amount);
            remaining -= amount;
        }

        if (remaining > 0)//没放完，视为失败（已放入的保留）
        {
            OnChanged?.Invoke();
            return false;
        }

        OnChanged?.Invoke();
        return true;
    }

    public bool UseItem(int index)//使用某格道具：数量 -1，归零则清空
    {
        InventoryItem item = GetItem(index);
        if (item == null)
            return false;

        if (item.RemoveStack())
            slots[index] = null;

        OnChanged?.Invoke();
        return true;
    }

    private void GiveStartingItems()//开局发放测试道具
    {
        for (int i = 0; i < startingItems.Count; i++)
        {
            ItemData data = startingItems[i];
            if (data == null)
                continue;

            int count = i < startingCounts.Count ? startingCounts[i] : 1;
            AddItem(data, Mathf.Max(1, count));
        }
    }
}
