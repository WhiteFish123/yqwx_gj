using UnityEngine;

// 格子种类：只描述“这是什么地面”，不含任何通行规则。
// 对应地图上的四种瓦片：墙 / 一楼 / 楼梯 / 二楼。
public enum CellKind
{
    Blocked = 0,// 墙、地图外、不可站立的虚空
    Floor1 = 1, // 一楼地面
    Stairs = 2, // 楼梯
    Floor2 = 3, // 二楼地面
}

// 一个格子的通行信息。Region 是核心：同号表示同一块连续地面。
public readonly struct CellInfo
{
    public readonly int Region;// 0 = 不可进入；>0 = 区域编号
    public readonly int Floor;// 0 = 非地面（墙或楼梯）；1 = 一楼；2 = 二楼
    public readonly bool IsStair;// 是不是楼梯格
    public readonly Vector2Int Axis;// 楼梯轴向：(0,1) 竖梯，(1,0) 横梯；非楼梯时无意义

    public CellInfo(int region, int floor, bool isStair, Vector2Int axis)
    {
        Region = region;
        Floor = floor;
        IsStair = isStair;
        Axis = axis;
    }

    // 不可进入的格子：墙、地图外
    public static CellInfo Blocked => default;

    // 能不能站人
    public bool CanStand => Region != 0;
}
