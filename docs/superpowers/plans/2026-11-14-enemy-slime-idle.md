# Enemy / Enemy_Slime / Idle 接入实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 subagent-driven-development（推荐）或 executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 让场景中的 `Enemy_Slime` 通过状态机进入 Idle 状态并播放 `Slime_Idle` 动画。

**架构：** `Enemy`（MonoBehaviour）持有 `Animator` 与 `StateMachine`；`Enemy_Slime` 在 `Awake` 创建 `Enemy_IdleState`、在 `Start` 调用 `Initialize`；`EnemyState` 继承已有的 `EntityState` 并注入 `Enemy`。敌人代码放入独立程序集 `WhiteFish.Enemy`，使 EditMode 测试可引用。

**技术栈：** Unity 6000.0.40f1、C#、Unity Test Framework（NUnit，EditMode）、unityMCP。

**规格：** `docs/superpowers/specs/2026-11-14-enemy-slime-idle-design.md`

## 全局约束

- 不使用命名空间；注释为中文
- 路径：`Assets/Scripts/WhiteFish/Enemy/`、`Assets/Tests/EditMode/`
- 动画参数名必须是 `"Idle"`（大小写逐字一致）
- 不新建 `Entity`、`Rigidbody2D`、`Collider2D`、`idleTime` 或其他状态类
- 不修改 `Enemy_Slime.controller` 的状态与过渡
- 不执行任何 git commit
- 交付文件：`WhiteFish.Enemy.asmdef`、`Enemy.cs`、`Enemy_Slime.cs`、`EnemyStates/EnemyState.cs`、`EnemyStates/Enemy_IdleState.cs`、`EnemySlimeIdleTests.cs`，并修改 `WhiteFish.StateMachine.Tests.asmdef`

## 审查重点（Review Focus）

1. 代码里 `animBoolName` 写成 `"idle"` 而 Controller 里是 `"Idle"` → 运行时刷 "Parameter 'Idle' does not exist"。由任务 1 的 `IdleState_AnimBoolNameMatchesController` 与 `Controller_HasIdleBoolParameter` 钉住。
2. `Animator` 在子对象 `Animation` 上，若代码用 `GetComponent` 而非 `GetComponentInChildren` → `anim` 为 null，`SetBool` 静默跳过且状态机看似正常。由任务 1 测试以「根对象 + 子对象 Animator」结构构造钉住。
3. `Enemy_Slime.Awake` 若忘记调用 `base.Awake()` → `stateMachine` 为 null，`new Enemy_IdleState` 抛 NRE。由任务 2 的 `Start_EntersIdleState` 失败暴露。
4. EditMode 下 `AddComponent` 不触发生命周期方法，测试若忘记反射调用 → 断言拿到 null 而误判实现有错。由任务 1 的 `InvokeLifecycle` 辅助方法钉住。
5. `Enemy.Update` 在 `stateMachine` 未初始化时调用会 NRE；依赖 `Start` 先于首次 `Update` 的顺序保证，不做额外防御，由任务 3 的 Play 模式验证覆盖。

---

### 任务 1：建立敌人程序集与测试基座（预期红）

**文件：**
- 创建：`Assets/Scripts/WhiteFish/Enemy/WhiteFish.Enemy.asmdef`
- 创建：`Assets/Scripts/WhiteFish/Enemy/Enemy.cs`
- 创建：`Assets/Scripts/WhiteFish/Enemy/Enemy_Slime.cs`
- 创建：`Assets/Scripts/WhiteFish/Enemy/EnemyStates/EnemyState.cs`
- 创建：`Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_IdleState.cs`
- 修改：`Assets/Tests/EditMode/WhiteFish.StateMachine.Tests.asmdef`
- 测试：`Assets/Tests/EditMode/EnemySlimeIdleTests.cs`

- [ ] **步骤 1：创建敌人程序集定义**

`Assets/Scripts/WhiteFish/Enemy/WhiteFish.Enemy.asmdef`：

```json
{
    "name": "WhiteFish.Enemy",
    "rootNamespace": "",
    "references": [
        "WhiteFish.StateMachine"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **步骤 2：让测试程序集引用敌人程序集**

修改 `Assets/Tests/EditMode/WhiteFish.StateMachine.Tests.asmdef` 的 `references` 数组，加入 `"WhiteFish.Enemy"`：

```json
"references": [
    "WhiteFish.StateMachine",
    "WhiteFish.Enemy",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
]
```

- [ ] **步骤 3：创建四个空壳脚本**

`Enemy.cs`：

```csharp
using UnityEngine;

public class Enemy : MonoBehaviour
{
}
```

`Enemy_Slime.cs`：

```csharp
public class Enemy_Slime : Enemy
{
}
```

`EnemyState.cs`：

```csharp
public class EnemyState : EntityState
{
    public EnemyState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(stateMachine, animBoolName)
    {
    }
}
```

`Enemy_IdleState.cs`：

```csharp
public class Enemy_IdleState : EnemyState
{
    public Enemy_IdleState(Enemy enemy, StateMachine stateMachine, string animBoolName)
        : base(enemy, stateMachine, animBoolName)
    {
    }
}
```

空壳必须保留上述构造函数：`EntityState` 没有无参构造，派生类不调用 `base(...)` 无法编译，红阶段应当是「成员缺失」而不是「语法错误」。

- [ ] **步骤 4：编写失败的测试**

`Assets/Tests/EditMode/EnemySlimeIdleTests.cs`：

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class EnemySlimeIdleTests
{
    private const string ControllerPath = "Assets/Animations/AnimatorControllers/Enemy_Slime.controller";

    private GameObject root;

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

        Animator animator = animation.AddComponent<Animator>();
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

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
```

- [ ] **步骤 5：运行测试验证失败**

调用 `unityMCP_refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`, `wait_for_ready: true`），然后 `unityMCP_run_tests`（`mode: "EditMode"`, `include_details: true`）。

预期：编译失败时 `run_tests` 会返回 `total: 0`（与状态机核心红阶段一致），以步骤 6 的 `read_console` 输出为准；若编译通过但断言失败，则为测试失败。

- [ ] **步骤 6：确认失败原因正确**

调用 `unityMCP_read_console`（`types: ["error"]`）。

预期错误为 `CS1061`（成员不存在）。若出现 `CS0246`（找不到类型 `Enemy_Slime` 或 `EntityState`），说明 asmdef 引用或程序集名有问题，先修 asmdef 直到错误类型变为成员缺失。

---

### 任务 2：实现四个脚本（预期绿）

**文件：**
- 修改：`Assets/Scripts/WhiteFish/Enemy/Enemy.cs`
- 修改：`Assets/Scripts/WhiteFish/Enemy/Enemy_Slime.cs`
- 修改：`Assets/Scripts/WhiteFish/Enemy/EnemyStates/EnemyState.cs`
- 修改：`Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_IdleState.cs`

**Interfaces（后续任务与测试依赖）：**
- `Enemy.anim`：`public Animator anim { get; private set; }`
- `Enemy.stateMachine`：`public StateMachine stateMachine { get; private set; }`
- `Enemy.idleState`：`public Enemy_IdleState idleState;`
- `Enemy.Awake/Start/Update`：`protected virtual`
- `EnemyState.enemy`：`protected Enemy enemy`
- `Enemy_IdleState`：类体为空（仅构造函数）

- [ ] **步骤 1：实现 `Enemy`**

`Assets/Scripts/WhiteFish/Enemy/Enemy.cs`：

- `using UnityEngine;`
- `Awake`：`anim = GetComponentInChildren<Animator>();`、`stateMachine = new StateMachine();`
- `Start`：空方法体
- `Update`：`stateMachine.UpdateActiveState();`

`Update` 不再做空判断：`Enemy_Slime.Start` 一定在首次 `Update` 之前执行 `Initialize`。

- [ ] **步骤 2：实现 `Enemy_Slime`**

`Assets/Scripts/WhiteFish/Enemy/Enemy_Slime.cs`：

- `Awake`：先 `base.Awake();`，再 `idleState = new Enemy_IdleState(this, stateMachine, "Idle");`
- `Start`：先 `base.Start();`，再 `stateMachine.Initialize(idleState);`

- [ ] **步骤 3：实现 `EnemyState`**

`Assets/Scripts/WhiteFish/Enemy/EnemyStates/EnemyState.cs`：

- 字段 `protected Enemy enemy;`
- 构造：`base(stateMachine, animBoolName, enemy.anim)` 之后 `this.enemy = enemy;`
- 不覆写 `UpdateAnimationParameters`

- [ ] **步骤 4：实现 `Enemy_IdleState`**

`Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_IdleState.cs`：只保留构造函数透传给 `base(enemy, stateMachine, animBoolName)`，类体其余为空。

- [ ] **步骤 5：运行测试验证通过**

调用 `unityMCP_refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`, `wait_for_ready: true`），然后 `unityMCP_run_tests`（`mode: "EditMode"`, `include_details: true`, `include_failed_tests: true`）。

预期：13 个测试全部通过（原有 9 个 + 新增 4 个），失败 0。

- [ ] **步骤 6：确认无编译错误**

调用 `unityMCP_read_console`（`types: ["error", "warning"]`）。

预期：无 `CS` 开头的编译错误；`Parameter 'Idle' does not exist` 类警告不应出现（测试未进入 Play 模式）。

---

### 任务 3：场景挂载与 Play 模式验证

**文件：**
- 修改：`Assets/Scenes/SampleScene.unity`（通过 MCP 给 `Enemy_Slime` 添加组件）

- [ ] **步骤 1：给场景对象添加组件**

调用 `unityMCP_manage_gameobject`（`action: "modify"`, `target: "Enemy_Slime"`, `search_method: "by_name"`, `components_to_add: ["Enemy_Slime"]`）。

预期：返回成功，且 `Enemy_Slime` 组件的 `componentTypes` 中出现 `Enemy_Slime`。

- [ ] **步骤 2：保存场景**

调用 `unityMCP_manage_scene`（`action: "save"`）。

预期：保存成功。

- [ ] **步骤 3：确认组件与 Animator 引用关系**

调用 `unityMCP_execute_code`（`action: "execute"`）：

```csharp
var go = GameObject.Find("Enemy_Slime");
var slime = go.GetComponent<Enemy_Slime>();
var anim = go.GetComponentInChildren<Animator>();
return "component=" + (slime != null) + " | animatorInChildren=" + (anim != null)
     + " | controller=" + (anim == null || anim.runtimeAnimatorController == null ? "none" : anim.runtimeAnimatorController.name);
```

预期：`component=True | animatorInChildren=True | controller=Enemy_Slime`。

- [ ] **步骤 4：进入 Play 模式**

调用 `unityMCP_manage_editor`（`action: "play"`），随后用 `bash` 执行 `Start-Sleep -Seconds 3` 等待进入运行状态。

预期：编辑器进入 Play 模式，场景视图开始运行。

- [ ] **步骤 5：检查运行时状态与警告**

先调用 `unityMCP_execute_code`（`action: "execute"`）：

```csharp
var go = GameObject.Find("Enemy_Slime");
var slime = go.GetComponent<Enemy_Slime>();
var anim = go.GetComponentInChildren<Animator>();
return "state=" + (slime.stateMachine.currentState == null ? "null" : slime.stateMachine.currentState.GetType().Name)
     + " | idleBool=" + anim.GetBool("Idle")
     + " | playing=" + anim.GetCurrentAnimatorClipInfo(0).Length;
```

预期：`state=Enemy_IdleState | idleBool=True | playing=1`。

再调用 `unityMCP_read_console`（`types: ["error", "warning"]`）。

预期：没有 `Parameter 'Idle' does not exist`，没有 `NullReferenceException`。

- [ ] **步骤 6：退出 Play 模式**

调用 `unityMCP_manage_editor`（`action: "stop"`）。

预期：回到编辑模式，场景恢复。

---

## 验收对照

| 规格要求 | 覆盖任务 |
| --- | --- |
| 4 个脚本 + asmdef 编译通过 | 任务 2 步骤 5–6 |
| `Awake` 创建 `Enemy_IdleState` | 任务 1 测试 `Awake_CreatesIdleState` |
| `Start` 进入 Idle | 任务 1 测试 `Start_EntersIdleState` |
| `animBoolName == "Idle"` | 任务 1 测试 `IdleState_AnimBoolNameMatchesController` |
| Controller 存在 Bool 参数 `Idle` | 任务 1 测试 `Controller_HasIdleBoolParameter` |
| `Animator` 从子对象查找 | 任务 1 测试的 `CreateSlime()` 结构 + 任务 3 步骤 3 |
| Play 模式播放 `Slime_Idle` 无警告 | 任务 3 步骤 4–6 |
| 不新增 Entity / Rigidbody2D / idleTime | 文件结构仅列出的 6 个文件 |
| 不执行 git commit | 全局约束 |
