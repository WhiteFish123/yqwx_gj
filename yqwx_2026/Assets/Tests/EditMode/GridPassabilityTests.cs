using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

// 用真实 Tilemap 验证 GridPassability 的“三张图 → 区域表”转换：
// Floor = 一楼，SecondFloor = 二楼，Stairs = 楼梯，空 = 墙。
public class GridPassabilityTests
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

    private GameObject root;
    private Tilemap floor;
    private Tilemap secondFloor;
    private Tilemap stairs;
    private Tile tile;
    private GridPassability passability;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("PassTestRoot");

        Grid grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        tile = ScriptableObject.CreateInstance<Tile>();

        floor = MakeTilemap("Floor");
        secondFloor = MakeTilemap("SecondFloor");
        stairs = MakeTilemap("Stairs");

        Paint(MapText);

        passability = root.AddComponent<GridPassability>();
        passability.Configure(floor, secondFloor, stairs, Vector2Int.up);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);

        if (tile != null)
            Object.DestroyImmediate(tile);
    }

    [Test]
    public void Rebuild_ReadsAllThreeTilemaps()
    {
        Assert.IsTrue(passability.HasMap, "三张图有瓦片时应该建出区域表");

        Assert.AreEqual(1, passability.At(new Vector3Int(2, 0, 0)).Floor, "一楼");
        Assert.AreEqual(2, passability.At(new Vector3Int(2, 7, 0)).Floor, "二楼");
        Assert.IsTrue(passability.At(new Vector3Int(2, 3, 0)).IsStair, "楼梯");
        Assert.AreEqual(0, passability.At(new Vector3Int(0, 3, 0)).Region, "墙没有区域");
    }

    [Test]
    public void CanWalk_StairSide_Blocked()
    {
        Assert.IsFalse(passability.CanWalk(new Vector3Int(3, 3, 0), Vector2Int.left), "走廊横着进楼梯");
        Assert.IsFalse(passability.CanWalk(new Vector3Int(2, 3, 0), Vector2Int.right), "楼梯横着迈出去");
    }

    [Test]
    public void CanWalk_StairBottomAndTop_Allowed()
    {
        Assert.IsTrue(passability.CanWalk(new Vector3Int(2, 0, 0), Vector2Int.up), "梯底进");
        Assert.IsTrue(passability.CanWalk(new Vector3Int(2, 5, 0), Vector2Int.up), "梯顶出");
        Assert.IsTrue(passability.CanWalk(new Vector3Int(2, 6, 0), Vector2Int.down), "从二楼下楼");
    }

    [Test]
    public void CanWalk_InsideStair_Allowed()
    {
        Assert.IsTrue(passability.CanWalk(new Vector3Int(1, 3, 0), Vector2Int.right), "梯内横移");
    }

    [Test]
    public void CanWalk_CorridorToSecondFloor_Blocked()
    {
        Assert.IsFalse(passability.CanWalk(new Vector3Int(3, 6, 0), Vector2Int.left), "不能平着上二楼");
    }

    [Test]
    public void CanWalk_Corridor_Normal()
    {
        Assert.IsTrue(passability.CanWalk(new Vector3Int(3, 3, 0), Vector2Int.up));
        Assert.IsTrue(passability.CanWalk(new Vector3Int(4, 3, 0), Vector2Int.left));
    }

    [Test]
    public void CanChestEnter_SecondFloorToFirstFloor_Allowed()
    {
        Assert.IsTrue(passability.CanChestEnter(new Vector3Int(2, 6, 0), new Vector3Int(3, 6, 0)), "推下楼");
    }

    [Test]
    public void CanChestEnter_FirstFloorToSecondFloor_Blocked()
    {
        Assert.IsFalse(passability.CanChestEnter(new Vector3Int(3, 6, 0), new Vector3Int(2, 6, 0)), "不能推上楼");
    }

    [Test]
    public void NoTilemaps_FallsBackToPhysicsOnly()
    {
        GameObject bare = new GameObject("BareGrid");
        bare.AddComponent<Grid>();

        GridPassability empty = bare.AddComponent<GridPassability>();
        empty.Configure(null, null, null, Vector2Int.up);

        Assert.IsFalse(empty.HasMap, "没有地图时不应建表");
        Assert.IsTrue(empty.CanWalk(Vector3Int.zero, Vector2Int.right), "没配地图 → 退化成只受物理阻挡");
        Assert.IsFalse(empty.ShouldFall(Vector3Int.zero, Vector3Int.zero));

        Object.DestroyImmediate(bare);
    }

    // ===== 工具 =====

    private Tilemap MakeTilemap(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform);
        return go.AddComponent<Tilemap>();
    }

    // 第一行在最上面
    private void Paint(string ascii)
    {
        string[] rows = ascii.Trim().Split('\n');

        for (int i = 0; i < rows.Length; i++)
            rows[i] = rows[i].Trim();

        int height = rows.Length;

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;

            for (int x = 0; x < rows[row].Length; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                switch (rows[row][x])
                {
                    case '0':
                        floor.SetTile(cell, tile);
                        break;

                    case '1':
                        stairs.SetTile(cell, tile);
                        break;

                    case '3':
                        secondFloor.SetTile(cell, tile);
                        break;
                }
            }
        }
    }
}
