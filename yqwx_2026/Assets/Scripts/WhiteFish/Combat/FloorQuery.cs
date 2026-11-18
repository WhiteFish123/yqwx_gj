using UnityEngine;
using UnityEngine.Tilemaps;

//楼层判定：SecondFloor 上有瓦片的格子算二楼，其余算一楼。
//Enemy、StoneLineOfSight、Chest 原本各写了一份相同规则，统一收在这里，
//以后改成多层结构只需要动这一个文件。
public static class FloorQuery
{
    //该格属于几楼：2 = 二楼，1 = 一楼
    public static int FloorOf(Tilemap secondFloor, Vector3Int cell)
    {
        if (secondFloor == null)
            return 1;

        return secondFloor.HasTile(cell) ? 2 : 1;
    }

    //该格是不是二楼
    public static bool IsSecondFloor(Tilemap secondFloor, Vector3Int cell)
    {
        return secondFloor != null && secondFloor.HasTile(cell);
    }

    //两个格子是否在同一楼
    public static bool SameFloor(Tilemap secondFloor, Vector3Int a, Vector3Int b)
    {
        return FloorOf(secondFloor, a) == FloorOf(secondFloor, b);
    }
}
