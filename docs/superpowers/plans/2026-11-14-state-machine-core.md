# 状态机核心（StateMachine / EntityState）实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 subagent-driven-development（推荐）或 executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 在 `yqwx_2026` 中建立可复用的状态机地基（`StateMachine` + `EntityState`），供后续敌人/玩家系统继承。

**架构：** `StateMachine` 是纯 C# 类，持有当前 `EntityState` 并负责 Enter/Exit/Update 调度；`EntityState` 是抽象基类，提供计时器、动画 bool 开关、动画事件标记。两者不依赖任何 MonoBehaviour、Stats 或游戏逻辑。状态机代码放入独立 asmdef，使 EditMode 测试可以引用它。

**技术栈：** Unity 6000.0.40f1、C#（默认 .NET Standard 2.1）、Unity Test Framework 1.4.6、NUnit、unityMCP。

**规格：** `docs/superpowers/specs/2026-11-14-state-machine-core-design.md`

## 全局约束

- 命名空间：不使用（与 `Assets/Scripts/WhiteFish/AudioManager.cs` 一致）
- 注释：中文
- 文件位置：`Assets/Scripts/StateMachine/`、`Assets/Tests/EditMode/`
- 不引入 `Entity`、`Enemy`、`Entity_Stats`、具体状态类
- 不执行任何 git commit（用户明确要求）
- 最终交付文件：`StateMachine.cs`、`EntityState.cs`、两个 asmdef、一个测试文件

## 文件结构

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/StateMachine/WhiteFish.StateMachine.asmdef` | 把状态机类隔离进可被测试引用的程序集 |
| `Assets/Scripts/StateMachine/StateMachine.cs` | 状态持有与切换调度 |
| `Assets/Scripts/StateMachine/EntityState.cs` | 状态基类：生命周期、计时器、动画开关、动画事件标记 |
| `Assets/Tests/EditMode/WhiteFish.StateMachine.Tests.asmdef` | EditMode 测试程序集，引用状态机程序集与 TestRunner |
| `Assets/Tests/EditMode/StateMachineTests.cs` | 行为契约测试（TDD） |

## 审查重点（Review Focus）

1. `Initialize(null)` → 必须抛 `ArgumentNullException`，不是静默或 `NullReferenceException`。
2. `ChangeState(当前同一实例)` → 必须返回 `false`，且不调用 `Exit` / `Enter`（否则动画闪烁、计时器复位）。
3. `anim == null` 或 `animBoolName` 为空 → `Enter` / `Exit` 静默跳过，不得抛异常。
4. `SwitchOffStateMachine()` 之后任意 `ChangeState` → 返回 `false`，状态不变。
5. `Initialize` 成功后 `currentState` 永不为 `null` → `UpdateActiveState()` 不得有空引用。

已知限制：`stateTimer` 的递减依赖 `Time.deltaTime`，EditMode 下该值无意义，因此不做自动化断言，留待 PlayMode 中实际使用时人工确认。

---

### 任务 1：建立测试基座（预期红）

**文件：**
- 创建：`Assets/Scripts/StateMachine/WhiteFish.StateMachine.asmdef`
- 创建：`Assets/Tests/EditMode/WhiteFish.StateMachine.Tests.asmdef`
- 创建：`Assets/Scripts/StateMachine/StateMachine.cs`（仅空壳，见步骤 3）
- 创建：`Assets/Scripts/StateMachine/EntityState.cs`（仅空壳，见步骤 4）
- 测试：`Assets/Tests/EditMode/StateMachineTests.cs`

- [ ] **步骤 1：创建状态机程序集定义**

`Assets/Scripts/StateMachine/WhiteFish.StateMachine.asmdef`：

```json
{
    "name": "WhiteFish.StateMachine",
    "rootNamespace": "",
    "references": [],
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

`autoReferenced: true` 是必需的，保证 `Assembly-CSharp` 里的 MonoBehaviour 能继承 `EntityState`。

- [ ] **步骤 2：创建测试程序集定义**

`Assets/Tests/EditMode/WhiteFish.StateMachine.Tests.asmdef`：

```json
{
    "name": "WhiteFish.StateMachine.Tests",
    "rootNamespace": "",
    "references": [
        "WhiteFish.StateMachine",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **步骤 3：创建 `StateMachine` 空壳**

`Assets/Scripts/StateMachine/StateMachine.cs`：

```csharp
public class StateMachine
{
}
```

- [ ] **步骤 4：创建 `EntityState` 空壳**

`Assets/Scripts/StateMachine/EntityState.cs`：

```csharp
public abstract class EntityState
{
}
```

- [ ] **步骤 5：编写失败的测试**

`Assets/Tests/EditMode/StateMachineTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class StateMachineTests
{
    private class FakeState : EntityState
    {
        private readonly string stateName;
        private readonly List<string> log;

        public FakeState(StateMachine stateMachine, string stateName, List<string> log)
            : base(stateMachine, null, null)
        {
            this.stateName = stateName;
            this.log = log;
        }

        public override void Enter()
        {
            base.Enter();
            log.Add("enter:" + stateName);
        }

        public override void Exit()
        {
            base.Exit();
            log.Add("exit:" + stateName);
        }
    }

    [Test]
    public void Initialize_EntersStartState()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);

        machine.Initialize(idle);

        Assert.AreSame(idle, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void ChangeState_ExitsOldThenEntersNew()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);

        bool changed = machine.ChangeState(move);

        Assert.IsTrue(changed);
        Assert.AreSame(move, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle", "exit:idle", "enter:move" }, log);
    }

    [Test]
    public void ChangeState_ToSameState_ReturnsFalseAndDoesNothing()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        machine.Initialize(idle);

        bool changed = machine.ChangeState(idle);

        Assert.IsFalse(changed);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void ChangeState_ToNull_ReturnsFalse()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        machine.Initialize(idle);

        bool changed = machine.ChangeState(null);

        Assert.IsFalse(changed);
        Assert.AreSame(idle, machine.currentState);
    }

    [Test]
    public void SwitchOff_BlocksFurtherChanges()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);
        machine.SwitchOffStateMachine();

        bool changed = machine.ChangeState(move);

        Assert.IsFalse(changed);
        Assert.AreSame(idle, machine.currentState);
    }

    [Test]
    public void Initialize_NullStartState_Throws()
    {
        StateMachine machine = new StateMachine();

        Assert.Throws<System.ArgumentNullException>(() => machine.Initialize(null));
    }

    [Test]
    public void ChangeState_RaisesOnStateChangedWithNewState()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);
        EntityState raised = null;
        machine.OnStateChanged += state => raised = state;

        machine.ChangeState(move);

        Assert.AreSame(move, raised);
    }

    [Test]
    public void UpdateActiveState_CallsCurrentStateUpdate()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        CountingState idle = new CountingState(machine);
        machine.Initialize(idle);

        machine.UpdateActiveState();

        Assert.AreEqual(1, idle.updateCount);
    }

    private class CountingState : EntityState
    {
        public int updateCount;

        public CountingState(StateMachine stateMachine) : base(stateMachine) { }

        public override void Update()
        {
            base.Update();
            updateCount++;
        }
    }
}
```

`FakeState` 只用 `stateTimer`，不依赖 `Animator`；因此 `anim` 传 `null` 时若 `Enter`/`Exit` 抛异常，测试会直接失败。

- [ ] **步骤 6：运行测试验证失败**

调用 `unityMCP_refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`），然后 `unityMCP_run_tests`（`mode: "EditMode"`）。

预期：编译失败或全部测试失败 —— `StateMachine` 上没有 `currentState` / `Initialize` / `ChangeState`，`EntityState` 上没有对应构造函数与成员。这是预期的红。

- [ ] **步骤 7：确认失败原因正确**

调用 `unityMCP_read_console`（`types: ["error"]`），确认错误是 `CS0117` / `CS1061` / `CS1729` 一类「成员不存在」错误，而不是 asmdef 引用错误（`CS0246` 找不到类型）。

若出现 asmdef 引用错误，先修 asmdef 的 `references` / `name`，直到错误变成成员缺失。

---

### 任务 2：实现两个类（预期绿）

**文件：**
- 修改：`Assets/Scripts/StateMachine/StateMachine.cs`
- 修改：`Assets/Scripts/StateMachine/EntityState.cs`

**Interfaces：**
- 产出：`StateMachine.currentState`（`EntityState`，只读）、`StateMachine.canChangeState`（`bool`，外部只读）、`StateMachine.Initialize(EntityState)`、`StateMachine.ChangeState(EntityState) → bool`、`StateMachine.UpdateActiveState()`、`StateMachine.SwitchOffStateMachine()`、`StateMachine.OnStateChanged`（`event Action<EntityState>`）
- 产出：`EntityState` 构造 `(StateMachine stateMachine, string animBoolName = null, Animator anim = null)`、`virtual Enter/Update/Exit()`、`virtual UpdateAnimationParameters()`、`AnimationTrigger()`、`ResetTimer()`、`protected readonly stateMachine/animBoolName/anim`、`protected stateTimer/triggerCalled`

- [ ] **步骤 1：实现 `StateMachine`**

`Assets/Scripts/StateMachine/StateMachine.cs` 使用 `using System;` 与 `using UnityEngine;`。

要求：
- `public EntityState currentState { get; private set; }`
- `public bool canChangeState { get; private set; }`
- `public event Action<EntityState> OnStateChanged;`
- `Initialize`：`startState == null` 抛 `ArgumentNullException`；否则 `canChangeState = true`、赋值、`Enter()`。
- `ChangeState`：`canChangeState == false || newState == null || newState == currentState` 时返回 `false`；否则 `currentState.Exit()`、赋值、`Enter()`、`OnStateChanged?.Invoke(currentState)`、返回 `true`。
- `UpdateActiveState()`：`currentState.Update()`。
- `SwitchOffStateMachine()`：`canChangeState = false`。

- [ ] **步骤 2：实现 `EntityState`**

`Assets/Scripts/StateMachine/EntityState.cs` 使用 `using UnityEngine;`。

要求：
- 字段：`protected readonly StateMachine stateMachine`、`protected readonly string animBoolName`、`protected readonly Animator anim`、`protected float stateTimer`、`protected bool triggerCalled`。
- 构造：`public EntityState(StateMachine stateMachine, string animBoolName = null, Animator anim = null)`，赋值三个字段。
- `public virtual void Enter()`：`triggerCalled = false`；`SetAnimBool(true)`。
- `public virtual void Update()`：`stateTimer -= Time.deltaTime`；`UpdateAnimationParameters()`。
- `public virtual void Exit()`：`SetAnimBool(false)`。
- `public virtual void UpdateAnimationParameters()`：空方法体。
- `public void AnimationTrigger()`：`triggerCalled = true`。
- `public void ResetTimer()`：`stateTimer = 0f`。
- 私有辅助 `SetAnimBool(bool value)`：`anim == null || string.IsNullOrEmpty(animBoolName)` 时直接返回，否则 `anim.SetBool(animBoolName, value)`。

- [ ] **步骤 3：运行测试验证通过**

调用 `unityMCP_refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`），然后 `unityMCP_run_tests`（`mode: "EditMode"`, `include_details: true`）。

预期：7 个测试全部通过，失败 0。

- [ ] **步骤 4：确认无编译错误**

调用 `unityMCP_read_console`（`types: ["error", "warning"]`）。

预期：没有 `CS` 开头的编译错误。

---

### 任务 3：编辑器内冒烟验证

**文件：**
- 不新增或修改文件（只读验证）

- [ ] **步骤 1：确认状态机程序集已加载**

调用 `unityMCP_unity_reflect`（`action: "get_type"`, `class_name: "StateMachine"`）。

预期：返回类型信息，包含 `currentState`、`Initialize`、`ChangeState`、`UpdateActiveState`、`SwitchOffStateMachine`。

- [ ] **步骤 2：确认 `EntityState` 是抽象类**

调用 `unityMCP_unity_reflect`（`action: "get_type"`, `class_name: "EntityState"`）。

预期：类型标记为 abstract，成员包含 `Enter`、`Update`、`Exit`、`AnimationTrigger`、`ResetTimer`。

- [ ] **步骤 3：用 execute_code 跑一次真实切换**

调用 `unityMCP_execute_code`（`action: "execute"`），代码：

```csharp
var machine = new StateMachine();
return machine != null ? "StateMachine visible" : "null";
```

预期：返回 `StateMachine visible` 且无异常（证明程序集在编辑器运行时可见）。

若 `execute_code` 无法引用项目程序集（报 `CS0246`），记录该结果并在汇报中说明：冒烟验证改由 EditMode 测试结果承担，不影响交付。

---

## 验收对照

| 规格要求 | 覆盖任务 |
| --- | --- |
| 两个文件编译通过 | 任务 2 步骤 3–4 |
| `Initialize` 进入起始状态 | 任务 1 测试 `Initialize_EntersStartState` |
| `ChangeState` 先 Exit 再 Enter | 任务 1 测试 `ChangeState_ExitsOldThenEntersNew` |
| 拒绝 null / 同一状态 / 锁定 | 任务 1 测试 `ChangeState_ToNull_ReturnsFalse`、`ChangeState_ToSameState_ReturnsFalseAndDoesNothing`、`SwitchOff_BlocksFurtherChanges` |
| `Initialize(null)` 抛异常 | 任务 1 测试 `Initialize_NullStartState_Throws` |
| `OnStateChanged` 事件 | 任务 1 测试 `ChangeState_RaisesOnStateChangedWithNewState` |
| `anim` 为 null 不崩溃 | 任务 1 全部测试均以 `anim = null` 构造 |
| `UpdateActiveState` 调度 | 任务 1 测试 `UpdateActiveState_CallsCurrentStateUpdate` |
| 不新增 Enemy / 具体状态 | 文件结构仅列出的 5 个文件 |
