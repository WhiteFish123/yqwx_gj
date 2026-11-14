# 敌人死亡机制实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 subagent-driven-development（推荐）或 executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 让敌人拥有血量、能承受伤害，死亡后播放死亡动画、关闭碰撞并延迟销毁。

**架构：** 血量与死亡入口放在 `Enemy`（`TakeDamage` / `Die` / `deadState`）；死亡表现放在 `Enemy_DeadState`（关碰撞、`anim.Play`、锁状态机、延迟销毁）；`Enemy_Slime` 只负责创建具体状态并传入自己的动画名。

**技术栈：** Unity 6000.0.40f1、C#、Unity Test Framework（NUnit，EditMode）、unityMCP。

**规格：** `docs/superpowers/specs/2026-11-14-enemy-death-design.md`

## 全局约束

- 不使用命名空间；注释为中文
- 不新建 `Enemy_Health` 或任何血量组件
- 不添加 `Rigidbody2D`、不改 Controller 参数与过渡
- 死亡动画靠 `anim.Play(deadAnimName)`，clip 名由子类构造参数传入
- 不执行 git commit
- 交付：修改 `Enemy.cs`、`Enemy_Slime.cs`、`EnemySlimeIdleTests.cs`，新增 `EnemyStates/Enemy_DeadState.cs`

## 审查重点（Review Focus）

1. `Enemy_DeadState` 在构造时缓存 `Collider2D`，若组件不存在则为 `null` → 必须在 `Enter` 里判空，否则死亡时 NRE。由 `DeadState_DisablesCollider` 与实现中的判空钉住。
2. 测试未先 `Initialize` 就调 `TakeDamage` → `canChangeState` 仍为 `false`，`ChangeState` 静默返回 `false`，死亡状态切不进去而测试看似通过。由所有死亡用例的 `Awake` + `Start` 前提钉住。
3. 死后重复受伤 → 血量被减到负数、`Die` 被重复调用。由 `TakeDamage_AfterDeath_DoesNothing` 钉住。
4. 死亡后状态被锁 → 其他逻辑仍尝试切状态时必须返回 `false` 且不抛异常。由 `DeadState_LocksStateMachine` 钉住。
5. `anim.Play` 传入空或错误的 clip 名 → 静默无动画。由 `Enemy_Slime` 传 `"Slime_Dead"` 与 Controller 中实际状态名一致来钉住（任务 3 的 Play 验证覆盖）。

---

### 任务 1：建立失败测试与成员骨架（预期红）

**文件：**
- 修改：`Assets/Scripts/WhiteFish/Enemy/Enemy.cs`
- 创建：`Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_DeadState.cs`
- 修改：`Assets/Scripts/WhiteFish/Enemy/Enemy_Slime.cs`
- 测试：`Assets/Tests/EditMode/EnemySlimeIdleTests.cs`

- [ ] **步骤 1：给 `Enemy` 添加成员骨架**

在 `Enemy.cs` 中新增：

- `[Header("Health")] [SerializeField] protected float maxHealth = 3f;`
- `public float currentHealth { get; private set; }`
- `public bool isDead { get; private set; }`
- `public Enemy_DeadState deadState;`
- `public virtual void TakeDamage(float damage) { }`（空体）
- `protected virtual void Die() { }`（空体）
- `public void DestroyGameObjectWithDelay(float delay) => Destroy(gameObject, delay);`

`Awake` 中**不**赋值 `currentHealth`。

- [ ] **步骤 2：创建 `Enemy_DeadState` 骨架**

```csharp
//敌人死亡状态：关闭碰撞、播放死亡动画、锁死状态机并延迟销毁
public class Enemy_DeadState : EnemyState
{
    public Enemy_DeadState(Enemy enemy, StateMachine stateMachine, string deadAnimName, float destroyDelay = 2f)
        : base(enemy, stateMachine, null)
    {
    }
}
```

构造签名为最终签名，方法体暂空（不覆写 `Enter`）。

- [ ] **步骤 3：`Enemy_Slime` 创建死亡状态**

`Awake` 中在 `idleState` 之后追加：

```csharp
deadState = new Enemy_DeadState(this, stateMachine, "Slime_Dead", 2f);
```

- [ ] **步骤 4：追加 7 条测试**

在 `EnemySlimeIdleTests.cs` 中：

1. `CreateSlime()` 内、`root.AddComponent<Enemy_Slime>()` **之前**追加 `root.AddComponent<CapsuleCollider2D>();`
2. 新增实例方法（`CreateSlime()` 是实例方法，不能是静态辅助）：

```csharp
private Enemy_Slime CreateStartedSlime()
{
    Enemy_Slime slime = CreateSlime();
    InvokeLifecycle(slime, "Awake");
    InvokeLifecycle(slime, "Start");
    return slime;
}
```

3. 追加用例：

```csharp
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
```

- [ ] **步骤 5：运行测试验证失败**

`unityMCP_refresh_unity`（`mode: "force"`, `scope: "all"`, `compile: "request"`, `wait_for_ready: true`）→ `unityMCP_run_tests`（`mode: "EditMode"`, `include_details: true`）。

预期：编译通过；`Awake_InitializesHealthToMax`、`TakeDamage_*`、`DeadState_*` 共 7 条失败（`currentHealth` 为 0、状态未切换、碰撞未关闭），原有 14 条仍通过。

- [ ] **步骤 6：确认失败原因正确**

`unityMCP_read_console`（`types: ["error"]`）→ 应无 `CS` 编译错误。

若有编译错误，说明骨架签名与测试消费的成员不一致，先修骨架再继续。

---

### 任务 2：实现死亡逻辑（预期绿）

**文件：**
- 修改：`Assets/Scripts/WhiteFish/Enemy/Enemy.cs`
- 修改：`Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_DeadState.cs`

**Interfaces（测试依赖）：**
- `Enemy.currentHealth`、`Enemy.isDead`、`Enemy.deadState`、`Enemy.TakeDamage(float)`、`Enemy.DestroyGameObjectWithDelay(float)`
- `Enemy_DeadState(Enemy, StateMachine, string, float)` + `override void Enter()`

- [ ] **步骤 1：实现 `Enemy` 的血量与死亡**

- `Awake` 中在 `stateMachine = new StateMachine();` 之后追加 `currentHealth = maxHealth;`
- `TakeDamage`：`isDead` 为真或 `damage <= 0f` 时返回；否则 `currentHealth -= damage`；`currentHealth <= 0f` 时归零并调用 `Die()`
- `Die`：`isDead` 为真时返回；否则 `isDead = true` 后 `stateMachine.ChangeState(deadState)`

- [ ] **步骤 2：实现 `Enemy_DeadState.Enter`**

字段：`private readonly Collider2D col;`、`private readonly string deadAnimName;`、`private readonly float destroyDelay;`

构造：`col = enemy.GetComponent<Collider2D>();` 并保存另两个参数。

`Enter` 顺序固定：

```csharp
base.Enter();
if (col != null) col.enabled = false;
if (anim != null) anim.Play(deadAnimName);
stateMachine.SwitchOffStateMachine();
enemy.DestroyGameObjectWithDelay(destroyDelay);
```

- [ ] **步骤 3：运行测试验证通过**

`unityMCP_refresh_unity`（同上参数）→ `unityMCP_run_tests`（`mode: "EditMode"`, `include_details: true`, `include_failed_tests: true`）。

预期：21/21 通过（原有 14 + 新增 7），失败 0。

- [ ] **步骤 4：确认无编译错误与警告**

`unityMCP_read_console`（`types: ["error", "warning"]`）→ 无 `CS` 错误。

---

### 任务 3：Play 模式验证

- [ ] **步骤 1：进入 Play 模式**

`unityMCP_manage_editor`（`action: "play"`），然后 `bash` 执行 `Start-Sleep -Seconds 2`。

- [ ] **步骤 2：记录死亡前状态并造成致命伤害**

`unityMCP_execute_code`（`action: "execute"`）：

```csharp
var go = GameObject.Find("Enemy_Slime");
var slime = go.GetComponent<Enemy_Slime>();
var anim = go.GetComponentInChildren<Animator>();
var col = go.GetComponent<CapsuleCollider2D>();
string before = "hp=" + slime.currentHealth + " state=" + slime.stateMachine.currentState.GetType().Name
              + " colEnabled=" + col.enabled;
slime.TakeDamage(1f);
string afterHit = " | afterHit hp=" + slime.currentHealth + " dead=" + slime.isDead;
slime.TakeDamage(2f);
string afterKill = " | afterKill hp=" + slime.currentHealth + " dead=" + slime.isDead
                 + " state=" + slime.stateMachine.currentState.GetType().Name
                 + " colEnabled=" + col.enabled
                 + " clip=" + (anim.GetCurrentAnimatorClipInfo(0).Length > 0 ? anim.GetCurrentAnimatorClipInfo(0)[0].clip.name : "none");
return before + afterHit + afterKill;
```

预期：`hp=3 ... colEnabled=True | afterHit hp=2 dead=False | afterKill hp=0 dead=True state=Enemy_DeadState colEnabled=False clip=Slime_Dead`

- [ ] **步骤 3：等待销毁并确认对象消失**

`bash` 执行 `Start-Sleep -Seconds 3`，再 `unityMCP_execute_code`：

```csharp
return "found=" + (GameObject.Find("Enemy_Slime") != null);
```

预期：`found=False`

- [ ] **步骤 4：检查 Console**

`unityMCP_read_console`（`types: ["error", "warning"]`）→ 无 error。

- [ ] **步骤 5：退出 Play 模式**

`unityMCP_manage_editor`（`action: "stop"`）。

注意：Play 模式中对象已被 `Destroy`，退出后场景对象会恢复，无需重新保存场景。

---

## 验收对照

| 规格要求 | 覆盖 |
| --- | --- |
| 血量在 `Enemy` 上，`Awake` 初始化为 `maxHealth` | 任务 2 步骤 1 + `Awake_InitializesHealthToMax` |
| `TakeDamage` 扣血、非正伤害无效 | `TakeDamage_ReducesHealth`、`TakeDamage_ZeroOrNegative_DoesNothing` |
| 血空进入死亡状态 | `TakeDamage_Fatal_SwitchesToDeadState` |
| 死后免疫 | `TakeDamage_AfterDeath_DoesNothing` |
| 关闭碰撞 | `DeadState_DisablesCollider` + 任务 3 |
| 锁死状态机 | `DeadState_LocksStateMachine` |
| 播放死亡动画 | 任务 3 的 `clip=Slime_Dead` |
| 延迟销毁 | 任务 3 步骤 3 |
| 不新增血量组件 / Rigidbody2D / Controller 改动 | 全局约束 + 文件清单 |
