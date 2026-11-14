using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class StoneLineOfSightTests
{
    private GameObject root;
    private Grid grid;
    private LayerMask obstacleMask;
    private readonly List<GameObject> obstacles = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0)
            Assert.Ignore("Obstacle Layer 未创建");

        obstacleMask = 1 << layer;

        root = new GameObject("StoneSightTestRoot");
        grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in obstacles)
            if (go != null)
                Object.DestroyImmediate(go);

        obstacles.Clear();

        if (root != null)
            Object.DestroyImmediate(root);
    }

    private void AddObstacle(Vector2Int cell)//在指定格放一堵 0.9 见方的墙
    {
        GameObject go = new GameObject("Obstacle_" + cell.x + "_" + cell.y);
        go.layer = LayerMask.NameToLayer("Obstacle");
        go.transform.position = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);
        obstacles.Add(go);
    }

    private static Vector3Int Cell(int x, int y)
    {
        return new Vector3Int(x, y, 0);
    }

    [Test]
    public void IsVisibleInLine_TargetAtSeventhCell_ReturnsTrue()
    {
        Physics2D.SyncTransforms();

        Assert.IsTrue(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 7, Cell(7, 0)));
    }

    [Test]
    public void IsVisibleInLine_TargetAtEighthCell_ReturnsFalse()
    {
        Physics2D.SyncTransforms();

        Assert.IsFalse(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 7, Cell(8, 0)));
    }

    [Test]
    public void IsVisibleInLine_TargetOffsetOneCell_ReturnsFalse()
    {
        Physics2D.SyncTransforms();

        Assert.IsFalse(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 7, Cell(3, 1)));
    }

    [Test]
    public void IsVisibleInLine_WallBetween_ReturnsFalse()
    {
        AddObstacle(new Vector2Int(2, 0));
        Physics2D.SyncTransforms();

        Assert.IsFalse(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 7, Cell(3, 0)));
    }

    [Test]
    public void IsVisibleInLine_TargetBehind_ReturnsFalse()
    {
        Physics2D.SyncTransforms();

        Assert.IsFalse(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 7, Cell(-3, 0)));
    }

    [Test]
    public void IsVisibleInLine_TargetAtOrigin_ReturnsFalse()
    {
        Physics2D.SyncTransforms();

        Assert.IsFalse(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 7, Cell(0, 0)));
    }

    [Test]
    public void IsVisibleInLine_ZeroLength_ReturnsFalse()
    {
        Physics2D.SyncTransforms();

        Assert.IsFalse(StoneLineOfSight.IsVisibleInLine(
            grid, obstacleMask, Cell(0, 0), Vector2Int.right, 0, Cell(0, 0)));
    }
}
