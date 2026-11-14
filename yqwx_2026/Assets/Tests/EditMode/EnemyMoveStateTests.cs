using NUnit.Framework;

public class EnemyMoveStateTests
{
    [Test]
    public void NextIndex_Loop_WrapsAround()
    {
        int step = 1;

        Assert.AreEqual(1, Enemy_MoveState.NextIndex(0, ref step, 4, true));
        Assert.AreEqual(2, Enemy_MoveState.NextIndex(1, ref step, 4, true));
        Assert.AreEqual(3, Enemy_MoveState.NextIndex(2, ref step, 4, true));
        Assert.AreEqual(0, Enemy_MoveState.NextIndex(3, ref step, 4, true));
        Assert.AreEqual(1, step, "环绕路线不应改变往返方向");
    }

    [Test]
    public void NextIndex_TwoPointLine_BouncesBetweenBothEnds()
    {
        int step = 1;

        Assert.AreEqual(1, Enemy_MoveState.NextIndex(0, ref step, 2, false), "从起点走向另一端");
        Assert.AreEqual(1, step);

        Assert.AreEqual(0, Enemy_MoveState.NextIndex(1, ref step, 2, false), "到达端点后回弹");
        Assert.AreEqual(-1, step);

        Assert.AreEqual(1, Enemy_MoveState.NextIndex(0, ref step, 2, false), "再次回弹");
        Assert.AreEqual(1, step);
    }

    [Test]
    public void NextIndex_ThreePointLine_BouncesAtBothEnds()
    {
        int step = 1;

        Assert.AreEqual(1, Enemy_MoveState.NextIndex(0, ref step, 3, false));
        Assert.AreEqual(1, step);

        Assert.AreEqual(2, Enemy_MoveState.NextIndex(1, ref step, 3, false));
        Assert.AreEqual(1, step);

        Assert.AreEqual(1, Enemy_MoveState.NextIndex(2, ref step, 3, false), "到达末端后回到倒数第二格");
        Assert.AreEqual(-1, step);

        Assert.AreEqual(0, Enemy_MoveState.NextIndex(1, ref step, 3, false));
        Assert.AreEqual(-1, step);

        Assert.AreEqual(1, Enemy_MoveState.NextIndex(0, ref step, 3, false), "到达起点端后回到第二格");
        Assert.AreEqual(1, step);
    }
}
