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

        Assert.IsNotNull(animator.runtimeAnimatorController, "Controller not found: " + ControllerPath);

        root.AddComponent<CapsuleCollider2D>();
        root.AddComponent<Rigidbody2D>();

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

        Assert.IsNotNull(method, "鎵句笉鍒扮敓鍛藉懆鏈熸柟娉曪細" + methodName);
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

        Assert.AreSame(animator, slime.anim, "Enemy 搴旈€氳繃 GetComponentInChildren 鎵惧埌瀛愬璞′笂鐨?Animator");
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

        Assert.IsNotNull(controller, "Controller not found: " + ControllerPath);

        bool found = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == "Idle" && parameter.type == AnimatorControllerParameterType.Bool)
                found = true;
        }

        Assert.IsTrue(found, "Controller 缂哄皯 Bool 鍙傛暟 Idle");
    }

    [Test]
    public void IdleState_AnimBoolNameMatchesController()
    {
        Enemy_Slime slime = CreateSlime();
        InvokeLifecycle(slime, "Awake");

        FieldInfo field = typeof(EntityState).GetField(
            "animBoolName",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, "EntityState 缂哄皯 animBoolName 瀛楁");
        Assert.AreEqual("Idle", field.GetValue(slime.idleState));
    }

    [Test]
    public void Awake_InitializesHealthToMax()
    {
        Enemy_Slime slime = CreateSlime();
        InvokeLifecycle(slime, "Awake");

        Assert.AreEqual(1f, slime.currentHealth);
        Assert.IsFalse(slime.isDead);
    }

    [Test]
    public void TakeDamage_ReducesHealth()
    {
        Enemy_Slime slime = CreateStartedSlime();

        slime.TakeDamage(0.5f);

        Assert.AreEqual(0.5f, slime.currentHealth);
        Assert.IsFalse(slime.isDead);
    }

    [Test]
    public void TakeDamage_ZeroOrNegative_DoesNothing()
    {
        Enemy_Slime slime = CreateStartedSlime();

        slime.TakeDamage(0f);
        slime.TakeDamage(-1f);

        Assert.AreEqual(1f, slime.currentHealth);
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
        Assert.IsFalse(slime.isDead, "鐘舵€佹満灏氭湭鍒濆鍖栨椂鍒囦笉杩涙浜＄姸鎬侊紝涓嶅簲鐣欎笅 isDead 鏍囪");
    }

    [Test]
    public void TakeDamage_WithoutDeadState_LogsErrorAndDoesNotDie()
    {
        Enemy_Slime slime = CreateStartedSlime();
        slime.deadState = null;

        LogAssert.Expect(LogType.Error, new Regex("Enemy_Slime_Test"));//ASCII-only: the localized message is unreliable across encodings
        slime.TakeDamage(3f);

        Assert.IsFalse(slime.isDead);
    }

    [Test]
    public void IdleState_WithNoPatrolPath_StaysIdle()
    {
        Enemy_Slime slime = CreateStartedSlime();
        Assert.IsNull(slime.patrolPoints);

        FieldInfo timer = typeof(EntityState).GetField("stateTimer",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(timer, "EntityState 缂哄皯 stateTimer 瀛楁");
        timer.SetValue(slime.idleState, -1f);

        slime.idleState.Update();

        Assert.AreSame(slime.idleState, slime.stateMachine.currentState);
    }
}


