using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public class PatrolRouteTests
{
    private GameObject root;
    private Tile tile;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);

        if (tile != null)
            Object.DestroyImmediate(tile);
    }

    private PatrolRoute CreateRoute(params Vector2Int[] cells)
    {
        root = new GameObject("PatrolTest");
        Grid grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        GameObject child = new GameObject("PatrolPath");
        child.transform.SetParent(root.transform);

        Tilemap tilemap = child.AddComponent<Tilemap>();
        child.AddComponent<TilemapRenderer>();
        PatrolRoute route = child.AddComponent<PatrolRoute>();

        tile = ScriptableObject.CreateInstance<Tile>();
        foreach (Vector2Int c in cells)
            tilemap.SetTile(new Vector3Int(c.x, c.y, 0), tile);

        return route;
    }

    [Test]
    public void StraightLine_BuildsOrderedPoints()
    {
        PatrolRoute route = CreateRoute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(3, 0), new Vector2Int(4, 0));

        bool ok = route.TryBuildPath(new Vector3(2.5f, 0f, 0f), out List<Vector3> points, out bool isLoop);

        Assert.IsTrue(ok);
        Assert.AreEqual(5, points.Count);
        Assert.IsFalse(isLoop);
        Assert.AreEqual(new Vector3(0.5f, 0.5f, 0f), points[0]);
        Assert.AreEqual(new Vector3(4.5f, 0.5f, 0f), points[4]);
    }

    [Test]
    public void StraightLine_StartsFromEndNotNearestCell()
    {
        PatrolRoute route = CreateRoute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0));

        route.TryBuildPath(new Vector3(1.5f, 0.5f, 0f), out List<Vector3> points, out _);

        Assert.AreEqual(new Vector3(0.5f, 0.5f, 0f), points[0]);
    }

    [Test]
    public void Ring_DetectedAsLoop()
    {
        PatrolRoute route = CreateRoute(
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(1, 1), new Vector2Int(0, 1));

        bool ok = route.TryBuildPath(Vector3.zero, out List<Vector3> points, out bool isLoop);

        Assert.IsTrue(ok);
        Assert.AreEqual(4, points.Count);
        Assert.IsTrue(isLoop);
    }

    [Test]
    public void Branch_ReturnsFalse()
    {
        PatrolRoute route = CreateRoute(
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(2, 0), new Vector2Int(1, 1));

        LogAssert.Expect(LogType.Error, new Regex("分叉"));

        bool ok = route.TryBuildPath(Vector3.zero, out List<Vector3> points, out _);

        Assert.IsFalse(ok);
        Assert.IsNull(points);
    }

    [Test]
    public void SingleCell_ReturnsFalse()
    {
        PatrolRoute route = CreateRoute(new Vector2Int(0, 0));

        LogAssert.Expect(LogType.Error, new Regex("至少需要两个格子"));

        Assert.IsFalse(route.TryBuildPath(Vector3.zero, out _, out _));
    }

    [Test]
    public void EmptyTilemap_ReturnsFalse()
    {
        PatrolRoute route = CreateRoute();

        LogAssert.Expect(LogType.Error, new Regex("没有格子"));

        Assert.IsFalse(route.TryBuildPath(Vector3.zero, out _, out _));
    }

    [Test]
    public void IsolatedCell_Ignored()
    {
        PatrolRoute route = CreateRoute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(10, 0));

        LogAssert.Expect(LogType.Warning, new Regex("孤立"));

        bool ok = route.TryBuildPath(Vector3.zero, out List<Vector3> points, out bool isLoop);

        Assert.IsTrue(ok);
        Assert.AreEqual(3, points.Count);
        Assert.IsFalse(isLoop);
    }
}
