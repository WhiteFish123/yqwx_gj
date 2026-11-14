using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GridPathfinderTests
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
        root = new GameObject("PathTestRoot");
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

    private static int Manhatten(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    [Test]
    public void StraightLine_ReturnsShortestPath()
    {
        Physics2D.SyncTransforms();
        GridPathfinder finder = new GridPathfinder(grid, obstacleMask);

        List<Vector3Int> path = finder.FindPath(Vector3Int.zero, new Vector3Int(3, 0, 0));

        Assert.IsNotNull(path);
        Assert.AreEqual(4, path.Count);
        Assert.AreEqual(Vector3Int.zero, path[0]);
        Assert.AreEqual(new Vector3Int(3, 0, 0), path[path.Count - 1]);
    }

    [Test]
    public void Path_NeverDiagonal()
    {
        Physics2D.SyncTransforms();
        GridPathfinder finder = new GridPathfinder(grid, obstacleMask);

        List<Vector3Int> path = finder.FindPath(Vector3Int.zero, new Vector3Int(3, 2, 0));

        Assert.IsNotNull(path);
        for (int i = 1; i < path.Count; i++)
            Assert.AreEqual(1, Manhatten(path[i - 1], path[i]), "相邻路径点必须四向相邻");
    }

    [Test]
    public void Obstacle_IsRoutedAround()
    {
        AddObstacle(new Vector2Int(1, 0));
        Physics2D.SyncTransforms();
        GridPathfinder finder = new GridPathfinder(grid, obstacleMask);

        List<Vector3Int> path = finder.FindPath(Vector3Int.zero, new Vector3Int(3, 0, 0));

        Assert.IsNotNull(path);
        Assert.IsFalse(path.Contains(new Vector3Int(1, 0, 0)), "不应走障碍格");
        Assert.AreEqual(6, path.Count, "绕一格需多走两步");
    }

    [Test]
    public void Unreachable_ReturnsNull()
    {
        AddObstacle(new Vector2Int(1, 0));
        AddObstacle(new Vector2Int(0, 1));
        AddObstacle(new Vector2Int(-1, 0));
        AddObstacle(new Vector2Int(0, -1));
        Physics2D.SyncTransforms();
        GridPathfinder finder = new GridPathfinder(grid, obstacleMask);

        Assert.IsNull(finder.FindPath(Vector3Int.zero, new Vector3Int(5, 5, 0)));
    }

    [Test]
    public void FindAdjacentGoal_ReturnsReachableNeighbor()
    {
        AddObstacle(new Vector2Int(2, 0));
        Physics2D.SyncTransforms();
        GridPathfinder finder = new GridPathfinder(grid, obstacleMask);

        Vector3Int? cell = finder.FindAdjacentGoal(Vector3Int.zero, new Vector3Int(2, 0, 0));

        Assert.IsNotNull(cell);
        Assert.AreEqual(1, Manhatten(cell.Value, new Vector3Int(2, 0, 0)), "必须是目标的四向邻居");
        Assert.AreEqual(new Vector3Int(1, 0, 0), cell.Value);
    }
}
