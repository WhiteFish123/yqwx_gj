//背包格子上可执行的动作类型
public enum SlotAction
{
    None,//点不开：既不能装备也不是消耗品
    Equip,//装到手上
    Use//用掉一个（消耗品）
}

//背包物品的"能做什么"判定：纯函数，不依赖任何 UI，方便单测
public static class InventoryActions
{
    //根据物品数据决定这个格子该给什么动作
    public static SlotAction ActionFor(InventoryItem item)
    {
        if (item == null || item.data == null)
            return SlotAction.None;

        //消耗品优先给"使用"：血药这种既能拿在手上按 J 喝，也能在背包里点使用
        if (item.data.isConsumable)
            return SlotAction.Use;

        //非消耗品里有 heldPrefab 的才是能装到手上的东西（枪、刀、铁球、打火机）
        if (item.data.heldPrefab != null)
            return SlotAction.Equip;

        return SlotAction.None;
    }
}
