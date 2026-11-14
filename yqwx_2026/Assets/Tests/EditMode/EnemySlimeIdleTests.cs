using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class EnemySlimeIdleTests
{
    private const string ControllerPath = "Assets/Animations/AnimatorControllers/Enemy_Slime.controller";

    private GameObject root;
    private Animator animator;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    private Enemy_Slime CreateSlime()
    {
        root = new GameObject("Enemy_Slime_Test");

        GameObject animation = new GameObject("Animation");
        animation.transform.SetParent(root.transform);

        animator = animation.AddComponent<Animator>();
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

        Assert.IsNotNull(animator.runtimeAnimatorController, "找不到 Controller：" + ControllerPath);

        return root.AddComponent<Enemy_Slime>();
    }

    private static void InvokeLifecycle(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.IsNotNull(method, "找不到生命周期方法：" + methodName);
        method.Invoke(target, null);
    }

    [Test]
    public void Awake_CreatesIdleState()
    {
        Enemy_Slime slime = CreateSlime();

        InvokeLifecycle(slime, "Awake");

        Assert.IsNotNull(slime.idleState);
        Assert.IsInstanceOf<Enemy_IdleState>(slime.idleState);
    }

    [Test]
    public void Awake_ResolvesAnimatorFromChild()
    {
        Enemy_Slime slime = CreateSlime();

        InvokeLifecycle(slime, "Awake");

        Assert.AreSame(animator, slime.anim, "Enemy 应通过 GetComponentInChildren 找到子对象上的 Animator");
    }

    [Test]
    public void Start_EntersIdleState()
    {
        Enemy_Slime slime = CreateSlime();

        InvokeLifecycle(slime, "Awake");
        InvokeLifecycle(slime, "Start");

        Assert.AreSame(slime.idleState, slime.stateMachine.currentState);
    }

    [Test]
    public void Controller_HasIdleBoolParameter()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        Assert.IsNotNull(controller, "找不到 Controller：" + ControllerPath);

        bool found = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == "Idle" && parameter.type == AnimatorControllerParameterType.Bool)
                found = true;
        }

        Assert.IsTrue(found, "Controller 缺少 Bool 参数 Idle");
    }

    [Test]
    public void IdleState_AnimBoolNameMatchesController()
    {
        Enemy_Slime slime = CreateSlime();
        InvokeLifecycle(slime, "Awake");

        FieldInfo field = typeof(EntityState).GetField(
            "animBoolName",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, "EntityState 缺少 animBoolName 字段");
        Assert.AreEqual("Idle", field.GetValue(slime.idleState));
    }
}
