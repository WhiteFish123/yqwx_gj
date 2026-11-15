using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class YSortTests
{
    private GameObject root;
    private GameObject body;
    private SpriteRenderer bodyRenderer;
    private YSort ysort;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("YSort_Test");

        body = new GameObject("Animation");
        body.transform.SetParent(root.transform);
        bodyRenderer = body.AddComponent<SpriteRenderer>();

        ysort = root.AddComponent<YSort>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    private void InvokeAwake()//EditMode 下 AddComponent 不会自动跑 Awake
    {
        MethodInfo m = typeof(YSort).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "YSort 缺少方法：Awake");
        m.Invoke(ysort, null);
    }

    private void InvokeLateUpdate()
    {
        MethodInfo m = typeof(YSort).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "YSort 缺少方法：LateUpdate");
        m.Invoke(ysort, null);
    }

    private void InvokeRefresh()
    {
        MethodInfo m = typeof(YSort).GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(m, "YSort 缺少方法：Refresh");
        m.Invoke(ysort, null);
    }

    private void SetField(string name, object value)
    {
        FieldInfo f = typeof(YSort).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, "YSort 缺少字段：" + name);
        f.SetValue(ysort, value);
    }

    private SpriteRenderer AddChildSprite(string childName, int order)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(root.transform);
        SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }

    [Test]
    public void LateUpdate_YGreaterGetsLowerOrder()
    {
        InvokeAwake();

        root.transform.position = new Vector3(0f, 1f, 0f);
        InvokeLateUpdate();
        int orderY1 = bodyRenderer.sortingOrder;

        root.transform.position = new Vector3(0f, 0f, 0f);
        InvokeLateUpdate();
        int orderY0 = bodyRenderer.sortingOrder;

        Assert.Less(orderY1, orderY0);
    }

    [Test]
    public void LateUpdate_YDifferenceOneCell_OrderDiffersByPrecision()
    {
        InvokeAwake();

        root.transform.position = new Vector3(0f, 0f, 0f);
        InvokeLateUpdate();
        int orderY0 = bodyRenderer.sortingOrder;

        root.transform.position = new Vector3(0f, 1f, 0f);
        InvokeLateUpdate();
        int orderY1 = bodyRenderer.sortingOrder;

        Assert.AreEqual(100, orderY0 - orderY1);
    }

    [Test]
    public void LateUpdate_KeepsChildRelativeOffset()
    {
        SpriteRenderer fire = AddChildSprite("FirePosition", 1);
        InvokeAwake();
        InvokeLateUpdate();

        Assert.AreEqual(bodyRenderer.sortingOrder + 1, fire.sortingOrder);
    }

    [Test]
    public void Refresh_CollectsInactiveChildren()
    {
        SpriteRenderer fire = AddChildSprite("FirePosition", 1);
        fire.gameObject.SetActive(false);
        InvokeAwake();
        InvokeLateUpdate();

        Assert.AreEqual(bodyRenderer.sortingOrder + 1, fire.sortingOrder);
    }

    [Test]
    public void LateUpdate_AppliesYOffset()
    {
        SetField("yOffset", 0f);
        InvokeAwake();
        root.transform.position = Vector3.zero;
        InvokeLateUpdate();
        int withoutOffset = bodyRenderer.sortingOrder;

        SetField("yOffset", 1f);
        InvokeLateUpdate();
        int withOffset = bodyRenderer.sortingOrder;

        Assert.AreEqual(100, withOffset - withoutOffset);
    }

    [Test]
    public void Refresh_CalledTwice_KeepsOriginalOffset()
    {
        InvokeAwake();
        root.transform.position = new Vector3(0f, 2f, 0f);
        InvokeLateUpdate();
        int firstOrder = bodyRenderer.sortingOrder;

        InvokeRefresh();
        InvokeLateUpdate();

        Assert.AreEqual(firstOrder, bodyRenderer.sortingOrder);
    }

    [Test]
    public void LateUpdate_SameY_ProducesSameOrder()
    {
        SpriteRenderer other = AddChildSprite("Other", 0);
        InvokeAwake();
        root.transform.position = new Vector3(0f, -1.5f, 0f);
        InvokeLateUpdate();

        Assert.AreEqual(bodyRenderer.sortingOrder, other.sortingOrder);
    }

    [Test]
    public void Refresh_NewChild_GetsItsOwnOffset()
    {
        InvokeAwake();
        root.transform.position = Vector3.zero;
        InvokeLateUpdate();

        SpriteRenderer extra = AddChildSprite("Extra", 5);
        InvokeRefresh();
        InvokeLateUpdate();

        Assert.AreEqual(bodyRenderer.sortingOrder + 5, extra.sortingOrder);
    }

    [Test]
    public void ItemPrefabs_HaveYSort()
    {
        string[] paths =
        {
            "Assets/Prefab/Item/Item_Knife.prefab",
            "Assets/Prefab/Item/Item_IronBall.prefab"
        };

        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "找不到预制体：" + path);
            Assert.IsNotNull(prefab.GetComponent<YSort>(), path + " 缺少 YSort，Instantiate 出来的物品不会参与排序");
        }
    }

    [Test]
    public void Awake_NoSpriteRenderer_LogsErrorAndDoesNotThrow()
    {
        GameObject empty = new GameObject("YSort_Empty");

        try
        {
            YSort emptySort = empty.AddComponent<YSort>();
            MethodInfo awake = typeof(YSort).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);

            LogAssert.Expect(LogType.Error, new Regex("没有任何 SpriteRenderer"));
            awake.Invoke(emptySort, null);

            MethodInfo lateUpdate = typeof(YSort).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.DoesNotThrow(() => lateUpdate.Invoke(emptySort, null));
        }
        finally
        {
            Object.DestroyImmediate(empty);
        }
    }
}
