# Enemy / Enemy_Slime / Idle 状态接入设计

日期：2026-11-14
项目：`D:\unity1\yqwx_gj\yqwx_2026`（Unity 6000.0.40f1，URP 2D）
前置：`docs/superpowers/specs/2026-11-14-state-machine-core-design.md`（StateMachine / EntityState 已完成）
参考：`D:\unity1\Udemy Course - RPG New1\Assets\Scripts\Enemy\`（三层继承 Entity → Enemy → Enemy_Slime）

## 目标

把场景中已有的 `Enemy_Slime` 对象接入状态机，让它在 `Idle` 状态运行 `Slime_Idle` 动画。这是敌人系统的第一个可运行切片，同时确立后续敌人（以及玩家）沿用的分层与命名。

## 现状（已核实）

| 项 | 事实 |
| --- | --- |
| 场景对象 | `Enemy_Slime`，根节点仅有 `Transform` |
| 动画节点 | 子对象 `Animation`，持有 `Animator` + `SpriteRenderer`（sprite `slime-idle-0_0`） |
| Animator Controller | `Assets/Animations/AnimatorControllers/Enemy_Slime.controller` |
| Controller 参数 | 仅 `Idle`（Bool，默认 `false`），大小写为 `Idle` |
| Controller 状态 | 仅 `Slime_Idle`，为默认状态，motion `Slime_Idle`，无任何过渡 |
| 物理组件 | 根节点与子节点均无 `Rigidbody2D`、无 `Collider2D` |

## 范围

新增四个脚本与一个程序集定义，并修改测试程序集的引用。

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/WhiteFish/Enemy/WhiteFish.Enemy.asmdef` | 敌人程序集，引用 `WhiteFish.StateMachine` |
| `Assets/Scripts/WhiteFish/Enemy/Enemy.cs` | 敌人基类：持有 Animator 与 StateMachine，驱动每帧更新 |
| `Assets/Scripts/WhiteFish/Enemy/Enemy_Slime.cs` | 具体敌人：创建所需状态并进入初始状态 |
| `Assets/Scripts/WhiteFish/Enemy/EnemyStates/EnemyState.cs` | 敌人状态基类：注入 `Enemy` 与 Animator |
| `Assets/Scripts/WhiteFish/Enemy/EnemyStates/Enemy_IdleState.cs` | Idle 状态（本次为空实现） |
| `Assets/Tests/EditMode/EnemySlimeIdleTests.cs` | EditMode 测试 |
| 修改 `Assets/Tests/EditMode/WhiteFish.StateMachine.Tests.asmdef` | `references` 增加 `"WhiteFish.Enemy"` |

## 非目标

- 不做 `Entity` 基类（不抽 `rb` / `col` / `facingDir` / 地面墙壁检测）
- 不做 `Move` / `Battle` / `Attack` / `Dead` / `Stunned` 状态
- 不引入 `idleTime`、`stateTimer` 的切换逻辑
- 不添加 `Rigidbody2D` / `Collider2D`
- 不修改 Animator Controller（不加状态、不加过渡）
- 不做索敌、伤害、血量、掉落

## 类设计

### Enemy

`public class Enemy : MonoBehaviour`，不加命名空间，中文注释。

| 成员 | 说明 |
| --- | --- |
| `public Animator anim { get; private set; }` | 由 `Awake` 通过 `GetComponentInChildren<Animator>()` 赋值 |
| `public StateMachine stateMachine { get; private set; }` | 由 `Awake` 赋值 `new StateMachine()` |
| `public Enemy_IdleState idleState;` | 由子类在 `Awake` 中赋值 |
| `protected virtual void Awake()` | 抓取上述引用并创建状态机 |
| `protected virtual void Start()` | 空实现，供子类覆写后调用 `base.Start()` |
| `protected virtual void Update()` | 调用 `stateMachine.UpdateActiveState()` |

`stateMachine` 对外只读是为了让 EditMode 测试能断言当前状态，同时不允许外部替换。

### Enemy_Slime

`public class Enemy_Slime : Enemy`。

| 方法 | 行为 |
| --- | --- |
| `protected override void Awake()` | `base.Awake()`；`idleState = new Enemy_IdleState(this, stateMachine, "Idle")` |
| `protected override void Start()` | `base.Start()`；`stateMachine.Initialize(idleState)` |

状态创建放在 `Awake`、进入初始状态放在 `Start`，与参考实现一致：保证所有状态在 `Initialize` 之前构造完毕。

### EnemyState

`public class EnemyState : EntityState`。

| 成员 | 说明 |
| --- | --- |
| `protected Enemy enemy` | 构造时注入 |
| `public EnemyState(Enemy enemy, StateMachine stateMachine, string animBoolName)` | 调用 `base(stateMachine, animBoolName, enemy.anim)` 后保存 `enemy` |

本次不覆写 `UpdateAnimationParameters`（没有速度/朝向需要同步）。

### Enemy_IdleState

`public class Enemy_IdleState : EnemyState`，构造直接透传父类。

**类体为空，不覆写任何方法**：Idle 的全部行为就是基类的 `Enter()`（打开 `Idle` 动画 bool）与每帧 `stateTimer` 递减。后续加入 `Move` 时需要设计时器与切换条件，届时再补 `Enter` / `Update` 覆写。

## 初始化时序

```
加载/实例化
  └─ Awake（Enemy）：anim = GetComponentInChildren<Animator>()；stateMachine = new StateMachine()
  └─ Awake（Enemy_Slime）：idleState = new Enemy_IdleState(this, stateMachine, "Idle")
  └─ Start（Enemy_Slime）：stateMachine.Initialize(idleState) → idleState.Enter() → anim.SetBool("Idle", true)
  └─ Update（Enemy）：stateMachine.UpdateActiveState() → idleState.Update() → stateTimer 递减（无人读取）
```

## 动画参数约定

- `Enemy_IdleState` 的 `animBoolName` 为 `"Idle"`，与 Controller 参数名逐字一致（含大写首字母）。
- 进入 Idle 时 `SetBool("Idle", true)`，退出时基类 `Exit()` 会 `SetBool("Idle", false)`。
- 由于 `Slime_Idle` 是默认状态且无过渡条件，参数取值不影响当前播放结果；该约定是为后续加入 `Move` 等状态做准备。

## 场景挂载步骤

1. 选中场景中的 `Enemy_Slime` 根对象
2. `Add Component` → `Enemy Slime`
3. 无需在 Inspector 拖任何引用（`Animator` 由代码从子对象查找）
4. 进入 Play 模式，确认动画持续播放 `Slime_Idle`，Console 无 "Parameter 'Idle' does not exist" 警告

## 验证

EditMode 测试 `EnemySlimeIdleTests`，覆盖三点：

1. **状态创建**：`Awake` 后 `idleState` 不为 `null`，且类型为 `Enemy_IdleState`。
2. **初始状态**：调用 `Awake` 与 `Start` 后，`stateMachine.currentState` 与 `idleState` 是同一实例。
3. **参数名正确**：`Enemy_Slime.controller` 的 `parameters` 中存在名为 `"Idle"` 的 Bool 参数，且 `Enemy_IdleState` 实例的 `animBoolName` 字段值等于 `"Idle"`（通过反射读取）。

第 3 点的资产检查用于防止「代码里写成 `"idle"` 但 Controller 里是 `"Idle"`」这类只在运行时刷警告、测试看不到的错误。

### 测试如何调用生命周期

EditMode 下 `AddComponent` 不会自动调用 `Awake` / `Start`。测试使用如下辅助方法通过反射调用：

```csharp
private static void InvokeLifecycle(object target, string methodName)
{
    MethodInfo method = target.GetType().GetMethod(
        methodName,
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    Assert.IsNotNull(method, "找不到生命周期方法：" + methodName);
    method.Invoke(target, null);
}
```

调用顺序固定为 `InvokeLifecycle(slime, "Awake")` → 断言 `idleState` → `InvokeLifecycle(slime, "Start")` → 断言 `currentState`。

不在 `EditMode` 断言 `anim.GetBool("Idle")`：Animator 的 bool 状态在非 Play 模式下不可靠（无 playable graph 初始化），因此改为断言 Controller 资产参数与控制内部字段值。

## 设计取舍

- **不建 `Entity`**：当前没有移动与物理需求，`rb` / `flip` / 地面检测全是死代码。等玩家系统需要共享时再抽，届时 `Enemy` 改成 `: Entity` 不影响状态类。
- **保留 `Enemy_Slime` 层**：后续 Slime 会覆写死亡、分裂等行为，提前分层避免以后改挂载的组件类型。
- **`Enemy` 暴露只读 `stateMachine`**：与参考实现的 `protected` 不同，多一个公开只读属性换取可测试性，外部仍无法替换实例。
- **不用 `idleTime`**：没有 `Move` 状态可切，计时器无处可去，属于 YAGNI。

## 验收标准

- 四个新脚本与 asmdef 编译通过，Console 无 error。
- EditMode 测试全部通过，包含新增 3 条与原有 9 条。
- 场景中 `Enemy_Slime` 挂上 `Enemy_Slime` 组件后，Play 模式播放 `Slime_Idle`，无参数缺失警告。
- 不新增 `Entity`、`Rigidbody2D`、`Collider2D`、其他状态类。
- 不执行任何 git commit。
