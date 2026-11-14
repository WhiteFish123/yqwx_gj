# 敌人死亡机制设计

日期：2026-11-14
项目：`D:\unity1\yqwx_gj\yqwx_2026`（Unity 6000.0.40f1，URP 2D）
前置：
- `docs/superpowers/specs/2026-11-14-state-machine-core-design.md`（StateMachine / EntityState）
- `docs/superpowers/specs/2026-11-14-enemy-slime-idle-design.md`（Enemy / Enemy_Slime / Idle）
参考：`D:\unity1\Udemy Course - RPG New1\Assets\Scripts\`（`Enemy_DeadState.cs`、`Entity_Health.cs`、`Enemy_Health.cs`、`EnemySlimeDeadState.cs`）

## 目标

让敌人拥有血量、能承受伤害、死亡后播放死亡动画、关闭碰撞并销毁。血量直接存在于 `Enemy` 上，不引入独立组件，后续敌人脚本继承 `Enemy` 即可获得完整能力。

## 现状（已核实）

| 项 | 事实 |
| --- | --- |
| `Enemy`（`Assets/Scripts/WhiteFish/Enemy/Enemy.cs`） | 有 `anim`（`GetComponentInChildren<Animator>`）、`stateMachine`、`idleState`、`Awake/Start/Update` |
| `Enemy_Slime` | `Awake` 创建 `idleState`，`Start` 调用 `Initialize` |
| 场景对象 `Enemy_Slime` | 根上有 `CapsuleCollider2D`（enabled=true、isTrigger=false），**无 `Rigidbody2D`** |
| Controller `Enemy_Slime.controller` | 参数仅 `Idle`（Bool）；状态 `Slime_Idle`（默认）、`Slime_Dead`（motion 正确）；**无任何过渡** |
| `Slime_Dead.anim` | 0.5 秒、4 帧 sprite 关键帧，`loopTime` 已改为 `false` |

## 范围

| 文件 | 改动 |
| --- | --- |
| `Assets/Scripts/WhiteFish/Enemy/Enemy.cs` | 修改：新增血量字段、`TakeDamage`、`Die`、`deadState` 字段、延迟销毁方法 |
| `Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_DeadState.cs` | 新增 |
| `Assets/Scripts/WhiteFish/Enemy/Enemy_Slime.cs` | 修改：`Awake` 中创建 `deadState` |
| `Assets/Tests/EditMode/EnemySlimeIdleTests.cs` | 修改：追加死亡相关用例 |

## 资源前置（实现前已具备）

| 资源 | 说明 |
| --- | --- |
| `Assets/Animations/AnimatorControllers/Enemy_Slime.controller` | 含 `Slime_Dead` 状态（motion 指向 `Slime_Dead.anim`），不加参数、不加过渡 |
| `Assets/Animations/Enemy/Slime/Slime_Dead.anim` | 0.5 秒 4 帧，`loopTime = false` |
| `Assets/Art/Test/Slime/slime-Sheet.png.meta` | sprite 表新增条目 |
| `Assets/Scenes/SampleScene.unity` | `Enemy_Slime` 根对象含 `CapsuleCollider2D` 与 `maxHealth: 3` |

这些资源是功能成立的必要条件：没有 Controller 的 `Slime_Dead` 状态，`anim.Play` 会静默失效；没有场景的 `CapsuleCollider2D`，关闭碰撞无对象可关。

## 非目标
- 不新建 `Enemy_Health` 或任何血量组件
- 不做击退、受击硬直、无敌帧、闪白/受击特效
- 不做掉落、任务进度、史莱姆分裂
- 不做 `Rigidbody2D` 与尸体物理（不加 `Rigidbody2D`）
- 不给 Controller 加参数或过渡（死亡动画用 `anim.Play` 直接播）
- 不做伤害来源、攻击判定、玩家攻击
- 不改 `EntityState` / `StateMachine`

## 类设计

### Enemy（扩展）

新增成员：

| 成员 | 签名 | 说明 |
| --- | --- | --- |
| `maxHealth` | `[SerializeField] protected float maxHealth = 3f;` | Inspector 可调，子类可读 |
| `currentHealth` | `public float currentHealth { get; private set; }` | 外部只读；测试可断言 |
| `isDead` | `public bool isDead { get; private set; }` | 死亡标记，防重复触发 |
| `deadState` | `public Enemy_DeadState deadState;` | 由子类在 `Awake` 中赋值 |
| `TakeDamage` | `public virtual void TakeDamage(float damage)` | 承受伤害入口，可被子类覆写 |
| `Die` | `protected virtual void Die()` | 死亡处理，可被子类覆写 |
| `DestroyGameObjectWithDelay` | `public void DestroyGameObjectWithDelay(float delay)` | 播放模式下调用 `Destroy(gameObject, delay)`；编辑模式直接返回（见设计取舍） |

`Awake` 追加一行 `currentHealth = maxHealth;`，其余不变。

`TakeDamage` 行为：

1. `isDead` 为真 → 直接返回
2. `damage <= 0f` → 直接返回
3. 扣减 `currentHealth`
4. `currentHealth <= 0f` 时把 `currentHealth` 归零并调用 `Die()`

不返回 bool：本设计没有调用方需要知道是否命中（参考项目返回 bool 是为了让 `Enemy_Health` 判断是否进战斗，本设计无此需求）。

`Die` 行为：

1. `isDead` 为真 → 直接返回
2. `deadState` 为 `null` → `Debug.LogError("敌人未配置死亡状态：<对象名>")` 后返回，`isDead` 保持 `false`（否则会变成血量 0、杀不死、也不消失的僵尸）
3. `isDead = true`
4. `stateMachine.ChangeState(deadState)`；返回 `false`（状态机未 `Initialize` 或已锁死）→ 回滚 `isDead = false`

`ChangeState` 对 `null` 目标与锁定状态返回 `false` 而不抛异常（核心状态机已实现该判定）。

### Enemy_DeadState（新增）

构造签名：`Enemy_DeadState(Enemy enemy, StateMachine stateMachine, string deadAnimName, float destroyDelay = 2f)`

- 调用 `base(enemy, stateMachine, null)`：死亡状态不参与动画 bool 体系
- 保存 `deadAnimName`、`destroyDelay`
- 缓存 `col = enemy.GetComponent<Collider2D>()`（可能为 null，需判空）

`Enter` 行为，顺序固定：

1. `base.Enter()`（仅复位 `triggerCalled`，因 `animBoolName` 为 null 不会调用 `SetBool`）
2. `if (col != null) col.enabled = false;`
3. `if (anim != null) anim.Play(deadAnimName);`
4. `stateMachine.SwitchOffStateMachine();`
5. `enemy.DestroyGameObjectWithDelay(destroyDelay);`

不覆写 `Update` / `Exit`。

**Idle 的动画 bool 如何关闭**：`stateMachine.ChangeState(deadState)` 会先调用 `idleState.Exit()`，即 `SetBool("Idle", false)`，再调用 `deadState.Enter()`。因此不需要在死亡状态里硬编码 `"Idle"`。

### Enemy_Slime（扩展）

`Awake` 中在创建 `idleState` 之后追加：

```csharp
deadState = new Enemy_DeadState(this, stateMachine, "Slime_Dead", 2f);
```

## 死亡时序

```
TakeDamage(伤害)  →  currentHealth -= damage
  ├─ 仍 > 0 → 结束
  └─ <= 0  → Die() → isDead = true → ChangeState(deadState)
                ├─ idleState.Exit()      → anim.SetBool("Idle", false)
                └─ deadState.Enter()
                     ├─ col.enabled = false
                     ├─ anim.Play("Slime_Dead")
                     ├─ SwitchOffStateMachine()
                     └─ Destroy(gameObject, 2f)
```

死亡后状态机被锁，任何 `ChangeState` 都返回 false，`Update` 仍会调用 `UpdateActiveState`，但留在 `Enemy_DeadState`。

## 动画约定

- 死亡动画通过 `anim.Play(clipName)` 直接触发，clip 名由子类构造参数提供（`Enemy_Slime` 传 `"Slime_Dead"`）
- Controller 不新增参数、不新增过渡，保持 `Idle` 参数与两个状态不变
- `Slime_Dead.anim` 必须 `loopTime = false`（已修改并验证）

## 测试

追加到 `Assets/Tests/EditMode/EnemySlimeIdleTests.cs`，复用现有 `CreateSlime()` 与 `InvokeLifecycle()`。

**前提**：除 `Awake_InitializesHealthToMax` 外，每个用例都必须先 `InvokeLifecycle(slime, "Awake")` 再 `InvokeLifecycle(slime, "Start")`。原因是 `StateMachine.canChangeState` 在 `Initialize` 之前为 `false`，`ChangeState` 会直接返回 `false`，死亡状态切不过去。

| 用例 | 断言 |
| --- | --- |
| `Awake_InitializesHealthToMax` | `currentHealth == 3f`、`isDead == false` |
| `TakeDamage_ReducesHealth` | 打 1 点后 `currentHealth == 2f`、`isDead == false` |
| `TakeDamage_ZeroOrNegative_DoesNothing` | 打 0 与 -1 后血量不变 |
| `TakeDamage_Fatal_SwitchesToDeadState` | 打满 3 点后 `isDead == true`、`stateMachine.currentState` 是 `deadState` |
| `TakeDamage_AfterDeath_DoesNothing` | 死亡后再打 1 点，血量仍为 0，状态仍是 `deadState` |
| `DeadState_DisablesCollider` | 死亡后 `CapsuleCollider2D.enabled == false` |
| `DeadState_LocksStateMachine` | 死亡后 `ChangeState(idleState)` 返回 false，`currentState` 仍是 `deadState` |

`CreateSlime()` 需要追加一个 `CapsuleCollider2D`，否则 `DeadState_DisablesCollider` 无处断言。测试里用 `root.AddComponent<CapsuleCollider2D>()`，位置在添加 `Enemy_Slime` 之前，保证 `DeadState` 构造时能拿到它。

不测试 `anim.Play` 的实际播放结果（EditMode 下不可靠，理由同 Idle 计划）；`Destroy` 延迟也不测，`Destroy(gameObject, 2f)` 在 EditMode 不会立即执行，`TearDown` 的 `DestroyImmediate` 负责清理。

## 设计取舍

- **血量放 `Enemy` 而非独立组件**：用户明确要求；当前没有多套血量规则（护甲、抗性、回复）的需求，独立组件带来的抽象成本现在没有回报。
- **`TakeDamage` 返回 `void`**：参考项目返回 `bool` 是为让上层判断"是否命中并需要进战斗"，本设计死亡以外没有观察者，YAGNI。
- **用 `anim.Play` 而非 bool + 过渡**：死亡是终态，不参与状态切换逻辑；`Play` 避免在 Controller 里配置过渡条件时出错（`hasExitTime`、`transitionDuration` 等），且其他敌人继承时只需传自己的 clip 名。
- **`destroyDelay` 做成构造参数默认 2 秒**：不同敌人尸体停留时间可能不同，暴露参数即可，不额外做配置系统。
- **`SwitchOffStateMachine` 保留**：与参考项目一致，防止死亡后其他逻辑（受击、AI）误切状态。
- **`DestroyGameObjectWithDelay` 在编辑模式下直接返回**：`Destroy` 在编辑模式会输出 error「Destroy may not be called from edit mode」，Unity Test Framework 将其判定为测试失败；而编辑模式没有时间流逝，延迟销毁本就无意义。Play 模式行为不变。

## 验收标准

- `Enemy.cs`、`Enemy_DeadState.cs`、`Enemy_Slime.cs` 编译通过，Console 无 error
- EditMode 测试全部通过（原有 14 条 + 新增 7 条）
- Play 模式下调用 `TakeDamage(3f)`：播放 `Slime_Dead`、`CapsuleCollider2D` 关闭、2 秒后对象消失、Console 无 error
- 不新增 `Enemy_Health`、`Rigidbody2D` 或任何 Controller 参数/过渡
- 不执行 git commit
