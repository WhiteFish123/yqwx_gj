using NUnit.Framework;
using UnityEngine;

// 用一张 8×5 的地图覆盖三张规则表：
//   走：CanWalk        推：CanChestEnter        掉：ShouldFall / FallLanding
//
// 地图（第一行在最上面，y 从下往上数）：
//   23300
//   23300
//   21100      2 = 墙   1 = 楼梯   0 = 一楼地板   3 = 二楼地板
//   21100
//   21100
//   21100
//   21100
//   20000
//
// 这是一段 2 格宽、5 格高的竖梯，梯顶接二楼阳台，右侧贴一条从底通到顶的一楼走廊。
public class GridRulesTests
{
    private const string MapText = @"
23300
23300
21100
21100
21100
21100
21100
20000";

    private RegionMap map;

    [SetUp]
    public void SetUp()
    {
        map = Load(MapText);
    }

    // ===== 走：CanWalk =====

    [Test]
    public void Walk_FloorToStairBottom_Allowed()
    {
        Assert.IsTrue(Walk(2, 0, Vector2Int.up), "从梯底顺着走进楼梯");
    }

    [Test]
    public void Walk_FloorToStairSide_Blocked()
    {
        Assert.IsFalse(Walk(3, 1, Vector2Int.left), "从走廊横着进楼梯");
        Assert.IsFalse(Walk(3, 3, Vector2Int.left), "楼梯中段侧面");
        Assert.IsFalse(Walk(3, 5, Vector2Int.left), "楼梯顶部侧面");
    }

    [Test]
    public void Walk_StairToFloorSide_Blocked()
    {
        Assert.IsFalse(Walk(2, 1, Vector2Int.right), "从楼梯横着迈到走廊");
        Assert.IsFalse(Walk(2, 4, Vector2Int.right), "楼梯中段侧面");
    }

    [Test]
    public void Walk_InsideStairLateral_Allowed()
    {
        Assert.IsTrue(Walk(1, 1, Vector2Int.right), "楼梯内部横向移动");
        Assert.IsTrue(Walk(2, 1, Vector2Int.left), "楼梯内部反向横移");
        Assert.IsTrue(Walk(1, 3, Vector2Int.right), "中段横移");
    }

    [Test]
    public void Walk_StairTopToSecondFloor_Allowed()
    {
        Assert.IsTrue(Walk(1, 5, Vector2Int.up), "梯顶左");
        Assert.IsTrue(Walk(2, 5, Vector2Int.up), "梯顶右");
    }

    [Test]
    public void Walk_SecondFloorToStairTop_Allowed()
    {
        Assert.IsTrue(Walk(1, 6, Vector2Int.down), "从二楼走回楼梯（下楼）");
    }

    [Test]
    public void Walk_FloorToSecondFloorDirect_Blocked()
    {
        Assert.IsFalse(Walk(3, 6, Vector2Int.left), "一楼走廊不能平着走上二楼阳台");
        Assert.IsFalse(Walk(3, 7, Vector2Int.left), "同上");
        Assert.IsFalse(Walk(2, 6, Vector2Int.right), "二楼也不能平着跳下走廊");
    }

    [Test]
    public void Walk_SameFloorCorridor_Allowed()
    {
        Assert.IsTrue(Walk(3, 3, Vector2Int.up), "走廊纵向");
        Assert.IsTrue(Walk(3, 3, Vector2Int.right), "走廊横向");
        Assert.IsTrue(Walk(4, 6, Vector2Int.left), "走廊横向");
        Assert.IsTrue(Walk(1, 0, Vector2Int.right), "一楼横向");
        Assert.IsTrue(Walk(1, 7, Vector2Int.right), "二楼阳台横向");
    }

    [Test]
    public void Walk_IntoWall_Blocked()
    {
        Assert.IsFalse(Walk(1, 0, Vector2Int.left), "走进左墙");
        Assert.IsFalse(Walk(2, 7, Vector2Int.up), "走出地图上边界");
    }

    // ===== 区域划分 =====

    [Test]
    public void Regions_SplitByKind()
    {
        int floor1 = map.At(new Vector3Int(1, 0, 0)).Region;
        int stairs = map.At(new Vector3Int(1, 1, 0)).Region;
        int floor2 = map.At(new Vector3Int(1, 6, 0)).Region;

        Assert.AreNotEqual(floor1, stairs, "一楼和楼梯必须分开");
        Assert.AreNotEqual(stairs, floor2, "楼梯和二楼必须分开");
        Assert.AreNotEqual(floor1, floor2, "一楼和二楼必须分开");
    }

    [Test]
    public void Regions_DisconnectedSameKind_Split()
    {
        RegionMap tiny = Load("101");

        Assert.AreNotEqual(
            tiny.At(new Vector3Int(0, 0, 0)).Region,
            tiny.At(new Vector3Int(2, 0, 0)).Region,
            "不连通的两块地面必须是不同区域");
    }

    [Test]
    public void Stairs_HaveFloorZeroAndAxis()
    {
        CellInfo stair = map.At(new Vector3Int(2, 3, 0));

        Assert.IsTrue(stair.IsStair);
        Assert.AreEqual(0, stair.Floor, "楼梯不是地面，楼层记 0");
        Assert.AreEqual(Vector2Int.up, stair.Axis);

        Assert.AreEqual(1, map.At(new Vector3Int(3, 3, 0)).Floor, "走廊是一楼");
        Assert.AreEqual(2, map.At(new Vector3Int(1, 6, 0)).Floor, "阳台是二楼");
        Assert.AreEqual(0, map.At(new Vector3Int(0, 3, 0)).Region, "墙没有区域");
    }

    // ===== 推：CanChestEnter =====

    [Test]
    public void Chest_SecondFloorToFirstFloor_Allowed()
    {
        Assert.IsTrue(Chest(2, 6, 3, 6), "从二楼把箱子推下去，必须允许");
    }

    [Test]
    public void Chest_FirstFloorToSecondFloor_Blocked()
    {
        Assert.IsFalse(Chest(3, 6, 2, 6), "不能把箱子推回二楼");
        Assert.IsFalse(Chest(3, 7, 2, 7), "同上");
    }

    [Test]
    public void Chest_IntoStairs_Blocked()
    {
        Assert.IsFalse(Chest(3, 3, 2, 3), "箱子不能推进楼梯");
    }

    [Test]
    public void Chest_IntoWall_Blocked()
    {
        Assert.IsFalse(Chest(1, 3, 0, 3), "箱子不能推进墙");
    }

    [Test]
    public void Chest_OnSameFloor_Allowed()
    {
        Assert.IsTrue(Chest(4, 3, 3, 3), "一楼走廊内正常推");
        Assert.IsTrue(Chest(2, 7, 1, 7), "二楼阳台内正常推");
    }

    // ===== 掉：ShouldFall / FallLanding =====

    [Test]
    public void ShouldFall_SecondFloorToFirstFloor_True()
    {
        Assert.IsTrue(ShouldFall(2, 6, 3, 6), "二楼推到一楼要掉");
    }

    [Test]
    public void ShouldFall_SameFloor_False()
    {
        Assert.IsFalse(ShouldFall(4, 3, 3, 3), "一楼推箱子不掉");
        Assert.IsFalse(ShouldFall(2, 7, 1, 7), "二楼内部推不掉");
    }

    [Test]
    public void FallLanding_FixedOffset_ReturnsCellBelow()
    {
        Vector3Int pushedTo = new Vector3Int(3, 6, 0);

        Assert.AreEqual(new Vector3Int(3, 5, 0), GridRules.FallLanding(pushedTo, 1, cell => true));
        Assert.AreEqual(new Vector3Int(3, 4, 0), GridRules.FallLanding(pushedTo, 2, cell => true));
    }

    [Test]
    public void FallLanding_BlockedBelow_StaysAtPushedCell()
    {
        Vector3Int pushedTo = new Vector3Int(3, 6, 0);

        Assert.AreEqual(pushedTo, GridRules.FallLanding(pushedTo, 1, cell => false), "下面被挡住就不掉");
        Assert.AreEqual(new Vector3Int(3, 5, 0), GridRules.FallLanding(pushedTo, 3, cell => cell.y == 5),
            "掉到被挡的前一格就停");
    }

    // ===== 工具 =====

    private bool Walk(int x, int y, Vector2Int dir)
    {
        Vector3Int from = new Vector3Int(x, y, 0);
        Vector3Int to = from + new Vector3Int(dir.x, dir.y, 0);

        return GridRules.CanWalk(map.At(from), map.At(to), dir);
    }

    private bool ShouldFall(int fromX, int fromY, int toX, int toY)
    {
        return GridRules.ShouldFall(
            map.At(new Vector3Int(fromX, fromY, 0)),
            map.At(new Vector3Int(toX, toY, 0)));
    }

    private bool Chest(int fromX, int fromY, int toX, int toY)
    {
        return GridRules.CanChestEnter(
            map.At(new Vector3Int(fromX, fromY, 0)),
            map.At(new Vector3Int(toX, toY, 0)));
    }

    // 解析 ASCII 地图：第一行在最上面
    private static RegionMap Load(string text)
    {
        string[] rows = text.Trim().Split('\n');

        for (int i = 0; i < rows.Length; i++)
            rows[i] = rows[i].Trim();

        int height = rows.Length;
        int width = rows[0].Length;
        CellKind[] kinds = new CellKind[width * height];

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;

            for (int x = 0; x < width; x++)
            {
                kinds[y * width + x] = rows[row][x] switch
                {
                    '0' => CellKind.Floor1,
                    '1' => CellKind.Stairs,
                    '2' => CellKind.Blocked,
                    '3' => CellKind.Floor2,
                    _ => CellKind.Blocked,
                };
            }
        }

        return RegionMap.Build(kinds, width, height, Vector3Int.zero, Vector2Int.up);
    }
}
