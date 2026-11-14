using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class EnemyVisionTests
{
    private GameObject root;
    private Grid grid;
    private LayerMask obstacleMask;
    private List<GameObject> obstacles;

    [SetUp]
    public void SetUp()
    {
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0)
            Assert.Ignore("Obstacle Layer 未创建");

        obstacleMask = 1 << layer;
        obstacles = new List<GameObject>();

        root = new GameObject("VisionTestRoot");
        grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in obstacles)
            if (go != null)
                Object.DestroyImmediate(go);

        if (root != null)
            Object.DestroyImmediate(root);
    }

    private void AddObstacle(Vector2Int cell)
    {
        GameObject go = new GameObject("Obstacle_" + cell.x + "_" + cell.y);
        go.layer = LayerMask.NameToLayer("Obstacle");
        go.transform.position = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);
        obstacles.Add(go);
    }

    [Test]
    public void ClearView_SeesSevenByThree()
    {
        EnemyVision vision = new EnemyVision(grid, obstacleMask);
        Physics2D.SyncTransforms();

        List<Vector2Int> cells = vision.GetVisibleCells(Vector3Int.zero, Vector2Int.right);

        Assert.AreEqual(21, cells.Count, "7 格长 × 3 排宽应共 21 格");
        Assert.IsTrue(cells.Contains(new Vector2Int(7, 0)), "最前方正中应可见");
        Assert.IsTrue(cells.Contains(new Vector2Int(1, -1)));
        Assert.IsTrue(cells.Contains(new Vector2Int(1, 1)));
        Assert.IsFalse(cells.Contains(Vector2Int.zero), "不应包含自身格");
    }

    [Test]
    public void Obstacle_BlocksOnlyThatRow()
    {
        AddObstacle(new Vector2Int(3, 0));
        Physics2D.SyncTransforms();

        EnemyVision vision = new EnemyVision(grid, obstacleMask);
        List<Vector2Int> cells = vision.GetVisibleCells(Vector3Int.zero, Vector2Int.right);

        Assert.IsFalse(cells.Contains(new Vector2Int(3, 0)), "障碍格本身不可见");
        Assert.IsFalse(cells.Contains(new Vector2Int(4, 0)), "障碍后方同排不可见");
        Assert.IsFalse(cells.Contains(new Vector2Int(7, 0)));

        Assert.IsTrue(cells.Contains(new Vector2Int(4, -1)), "下排不受影响");
        Assert.IsTrue(cells.Contains(new Vector2Int(4, 1)), "上排不受影响");
        Assert.IsTrue(cells.Contains(new Vector2Int(7, 1)));
    }

    [Test]
    public void FacingUp_BecomesVertical()
    {
        EnemyVision vision = new EnemyVision(grid, obstacleMask);
        Physics2D.SyncTransforms();

        List<Vector2Int> cells = vision.GetVisibleCells(Vector3Int.zero, Vector2Int.up);

        Assert.AreEqual(21, cells.Count);
        Assert.IsTrue(cells.Contains(new Vector2Int(0, 7)));
        Assert.IsTrue(cells.Contains(new Vector2Int(-1, 1)));
        Assert.IsTrue(cells.Contains(new Vector2Int(1, 1)));
        Assert.IsFalse(cells.Contains(new Vector2Int(7, 0)), "朝上时不应看到正右方");
    }

    [Test]
    public void FacingLeft_MirrorsHorizontally()
    {
        EnemyVision vision = new EnemyVision(grid, obstacleMask);
        Physics2D.SyncTransforms();

        List<Vector2Int> cells = vision.GetVisibleCells(Vector3Int.zero, Vector2Int.left);

        Assert.IsTrue(cells.Contains(new Vector2Int(-7, 0)));
        Assert.IsFalse(cells.Contains(new Vector2Int(1, 0)));
    }

    [Test]
    public void ZeroFacing_ReturnsEmpty()
    {
        EnemyVision vision = new EnemyVision(grid, obstacleMask);

        Assert.AreEqual(0, vision.GetVisibleCells(Vector3Int.zero, Vector2Int.zero).Count);
    }
}
