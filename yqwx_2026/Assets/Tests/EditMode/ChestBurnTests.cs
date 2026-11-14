using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ChestBurnTests
{
    private GameObject root;
    private SpriteRenderer body;
    private Chest chest;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Chest_Burn_Test");

        GameObject bodyObject = new GameObject("Animation");
        bodyObject.transform.SetParent(root.transform);
        body = bodyObject.AddComponent<SpriteRenderer>();

        chest = root.AddComponent<Chest>();
        InvokeAwake();
        SetField("burnDuration", 1f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    private void InvokeAwake()//EditMode 下 AddComponent 不会自动跑 Awake，需手动模拟
    {
        MethodInfo method = typeof(Chest).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "Chest 缺少方法：Awake");
        method.Invoke(chest, null);
    }

    private void SetField(string name, object value)
    {
        FieldInfo field = typeof(Chest).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "Chest 缺少字段：" + name);
        field.SetValue(chest, value);
    }

    private object GetField(string name)
    {
        FieldInfo field = typeof(Chest).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "Chest 缺少字段：" + name);
        return field.GetValue(chest);
    }

    private void Tick(float deltaTime)
    {
        MethodInfo method = typeof(Chest).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "Chest 缺少方法：Tick");
        method.Invoke(chest, new object[] { deltaTime });
    }

    [Test]
    public void Ignite_StartsBurningAndRaisesEvent()
    {
        int ignited = 0;
        chest.Ignited += c => ignited++;

        chest.Ignite();

        Assert.IsTrue(chest.IsBurning);
        Assert.IsFalse(chest.IsCharred);
        Assert.AreEqual(1f, chest.BurnRemaining, 0.0001f);
        Assert.AreEqual(1, ignited);
    }

    [Test]
    public void Ignite_WhenAlreadyBurning_DoesNothing()
    {
        int ignited = 0;
        chest.Ignited += c => ignited++;

        chest.Ignite();
        Tick(0.4f);
        chest.Ignite();

        Assert.AreEqual(1, ignited);
        Assert.AreEqual(0.6f, chest.BurnRemaining, 0.0001f);
        Assert.IsTrue(chest.IsBurning);
    }

    [Test]
    public void Tick_UntilZero_BecomesCharredAndRaisesEvent()
    {
        int finished = 0;
        chest.BurnFinished += c => finished++;

        chest.Ignite();
        Tick(0.5f);

        Assert.IsTrue(chest.IsBurning);
        Assert.IsFalse(chest.IsCharred);
        Assert.AreEqual(0.5f, chest.BurnRemaining, 0.0001f);

        Tick(0.6f);

        Assert.IsFalse(chest.IsBurning);
        Assert.IsTrue(chest.IsCharred);
        Assert.AreEqual(0f, chest.BurnRemaining, 0.0001f);
        Assert.AreEqual(1, finished);
    }

    [Test]
    public void Tick_AfterCharred_DoesNotRaiseEventAgain()
    {
        int finished = 0;
        chest.BurnFinished += c => finished++;

        chest.Ignite();
        Tick(2f);
        Tick(2f);

        Assert.AreEqual(1, finished);
        Assert.IsTrue(chest.IsCharred);
    }

    [Test]
    public void Ignite_WhenCharred_DoesNothing()
    {
        chest.Ignite();
        Tick(2f);

        chest.Ignite();

        Assert.IsFalse(chest.IsBurning);
        Assert.IsTrue(chest.IsCharred);
    }

    [Test]
    public void Tick_UntilZero_AppliesCharredColor()
    {
        Color charredColor = (Color)GetField("charredColor");

        chest.Ignite();
        Tick(2f);

        Assert.AreEqual(charredColor.r, body.color.r, 0.001f);
        Assert.AreEqual(charredColor.g, body.color.g, 0.001f);
        Assert.AreEqual(charredColor.b, body.color.b, 0.001f);
    }
}
