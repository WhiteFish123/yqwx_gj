using NUnit.Framework;
using UnityEngine;

public class ChestPushPullRulesTests
{
    private static readonly Vector2Int Facing = Vector2Int.right;

    private static bool NeverBlocked(Vector2Int cell)
    {
        return false;
    }

    private static bool NoChest(Vector2Int cell)
    {
        return false;
    }

    [Test]
    public void Resolve_SameAsFacing_ReturnsPush()
    {
        Assert.AreEqual(ChestMoveKind.Push, ChestPushPullRules.Resolve(Facing, Vector2Int.right));
    }

    [Test]
    public void Resolve_OppositeFacing_ReturnsPull()
    {
        Assert.AreEqual(ChestMoveKind.Pull, ChestPushPullRules.Resolve(Facing, Vector2Int.left));
    }

    [Test]
    public void Resolve_SideOrZero_ReturnsNone()
    {
        Assert.AreEqual(ChestMoveKind.None, ChestPushPullRules.Resolve(Facing, Vector2Int.up));
        Assert.AreEqual(ChestMoveKind.None, ChestPushPullRules.Resolve(Facing, Vector2Int.zero));
    }

    [Test]
    public void TryComputeTargets_Push_MovesPlayerIntoBoxCell()
    {
        bool ok = ChestPushPullRules.TryComputeTargets(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Push,
            out Vector2Int playerTarget, out Vector2Int chestTarget);

        Assert.IsTrue(ok);
        Assert.AreEqual(new Vector2Int(1, 0), playerTarget);
        Assert.AreEqual(new Vector2Int(2, 0), chestTarget);
    }

    [Test]
    public void TryComputeTargets_Pull_MovesPlayerBackAndBoxIntoPlayerCell()
    {
        bool ok = ChestPushPullRules.TryComputeTargets(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Pull,
            out Vector2Int playerTarget, out Vector2Int chestTarget);

        Assert.IsTrue(ok);
        Assert.AreEqual(new Vector2Int(-1, 0), playerTarget);
        Assert.AreEqual(new Vector2Int(0, 0), chestTarget);
    }

    [Test]
    public void TryComputeTargets_BoxNotInFront_ReturnsFalse()
    {
        bool ok = ChestPushPullRules.TryComputeTargets(
            new Vector2Int(0, 0), new Vector2Int(0, 1), Facing, ChestMoveKind.Push,
            out _, out _);

        Assert.IsFalse(ok);
    }

    [Test]
    public void CanGrab_ChestInFront_ReturnsTrue()
    {
        Assert.IsTrue(ChestPushPullRules.CanGrab(Vector2Int.zero, Facing, true));
    }

    [Test]
    public void CanGrab_NoChestInFront_ReturnsFalse()
    {
        Assert.IsFalse(ChestPushPullRules.CanGrab(Vector2Int.zero, Facing, false));
    }

    [Test]
    public void CanExecute_PushIntoWall_ReturnsFalse()
    {
        Vector2Int wall = new Vector2Int(2, 0);

        bool ok = ChestPushPullRules.CanExecute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Push,
            cell => cell == wall, NoChest);

        Assert.IsFalse(ok);
    }

    [Test]
    public void CanExecute_PullIntoWall_ReturnsFalse()
    {
        Vector2Int wall = new Vector2Int(-1, 0);

        bool ok = ChestPushPullRules.CanExecute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Pull,
            cell => cell == wall, NoChest);

        Assert.IsFalse(ok);
    }

    [Test]
    public void CanExecute_PushOntoAnotherChest_ReturnsFalse()
    {
        Vector2Int occupied = new Vector2Int(2, 0);

        bool ok = ChestPushPullRules.CanExecute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Push,
            NeverBlocked, cell => cell == occupied);

        Assert.IsFalse(ok);
    }

    [Test]
    public void CanExecute_ClearPath_ReturnsTrue()
    {
        Assert.IsTrue(ChestPushPullRules.CanExecute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Push,
            NeverBlocked, NoChest));

        Assert.IsTrue(ChestPushPullRules.CanExecute(
            new Vector2Int(0, 0), new Vector2Int(1, 0), Facing, ChestMoveKind.Pull,
            NeverBlocked, NoChest));
    }
}
