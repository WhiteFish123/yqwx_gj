using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

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

        root.AddComponent<CapsuleCollider2D>();

        return root.AddComponent<Enemy_Slime>();
    }

    private Enemy_Slime CreateStartedSlime()
    {
        Enemy_Slime slime = CreateSlime();
        InvokeLifecycle(slime, "Awake");
        InvokeLifecycle(slime, "Start");
        return slime;
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

    [Test]
    public void Awake_InitializesHealthToMax()
    {
        Enemy_Slime slime = CreateSlime();
        InvokeLifecycle(slime, "Awake");

        Assert.AreEqual(3f, slime.currentHealth);
        Assert.IsFalse(slime.isDead);
    }

    [Test]
    public void TakeDamage_ReducesHealth()
    {
        Enemy_Slime slime = CreateStartedSlime();

        slime.TakeDamage(1f);

        Assert.AreEqual(2f, slime.currentHealth);
        Assert.IsFalse(slime.isDead);
    }

    [Test]
    public void TakeDamage_ZeroOrNegative_DoesNothing()
    {
        Enemy_Slime slime = CreateStartedSlime();

        slime.TakeDamage(0f);
        slime.TakeDamage(-1f);

        Assert.AreEqual(3f, slime.currentHealth);
    }

    [Test]
    public void TakeDamage_Fatal_SwitchesToDeadState()
    {
        Enemy_Slime slime = CreateStartedSlime();

        slime.TakeDamage(3f);

        Assert.IsTrue(slime.isDead);
        Assert.AreSame(slime.deadState, slime.stateMachine.currentState);
    }

    [Test]
    public void TakeDamage_AfterDeath_DoesNothing()
    {
        Enemy_Slime slime = CreateStartedSlime();
        slime.TakeDamage(3f);

        slime.TakeDamage(1f);

        Assert.AreEqual(0f, slime.currentHealth);
        Assert.AreSame(slime.deadState, slime.stateMachine.currentState);
    }

    [Test]
    public void DeadState_DisablesCollider()
    {
        Enemy_Slime slime = CreateStartedSlime();

        slime.TakeDamage(3f);

        CapsuleCollider2D col = root.GetComponent<CapsuleCollider2D>();
        Assert.IsNotNull(col);
        Assert.IsFalse(col.enabled);
    }

    [Test]
    public void DeadState_LocksStateMachine()
    {
        Enemy_Slime slime = CreateStartedSlime();
        slime.TakeDamage(3f);

        bool changed = slime.stateMachine.ChangeState(slime.idleState);

        Assert.IsFalse(changed);
        Assert.AreSame(slime.deadState, slime.stateMachine.currentState);
    }

    [Test]
    public void TakeDamage_BeforeInitialize_DoesNotStayDead()
    {
        Enemy_Slime slime = CreateSlime();
        InvokeLifecycle(slime, "Awake");

        slime.TakeDamage(3f);

        Assert.AreEqual(0f, slime.currentHealth);
        Assert.IsFalse(slime.isDead, "状态机尚未初始化时切不进死亡状态，不应留下 isDead 标记");
    }

    [Test]
    public void TakeDamage_WithoutDeadState_LogsErrorAndDoesNotDie()
    {
        Enemy_Slime slime = CreateStartedSlime();
        slime.deadState = null;

        LogAssert.Expect(LogType.Error, new Regex("敌人未配置死亡状态"));
        slime.TakeDamage(3f);

        Assert.IsFalse(slime.isDead);
    }
}
