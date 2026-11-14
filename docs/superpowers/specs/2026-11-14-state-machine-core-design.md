# 状态机核心（StateMachine / EntityState）设计

日期：2026-11-14
项目：`D:\unity1\yqwx_gj\yqwx_2026`（Unity 6000.0.40f1，URP 2D）
参考实现：`D:\unity1\Udemy Course - RPG New1\Assets\Scripts\StateMachine\`

## 目标

为后续敌人系统（以及玩家系统）提供一套可直接继承的状态机地基：能在任意 GameObject 上运行，状态可进入、每帧更新、退出，并可选地驱动 Animator 的 bool 参数。

## 范围

本次只交付两个文件：

- `Assets/Scripts/StateMachine/StateMachine.cs`
- `Assets/Scripts/StateMachine/EntityState.cs`

风格与现有 `Assets/Scripts/WhiteFish/AudioManager.cs` 保持一致：无命名空间、中文注释。两个类放入独立 asmdef `WhiteFish.StateMachine`（`autoReferenced: true`），使 EditMode 测试可引用，同时 `Assembly-CSharp` 中的 MonoBehaviour 仍可直接继承 `EntityState`。

## 非目标

- 不做 `Entity`、`Enemy`、`Player` 等 MonoBehaviour 层
- 不做 `EnemyState` 或任何具体状态（Idle / Move / Battle / Attack / Dead / Stunned）
- 不引入 `Entity_Stats`、`SyncAttackSpeed`、伤害/血量/战斗系统
- 不做状态名字符串常量表、编辑器可视化、序列化、存档

## StateMachine

纯 C# 类（非 MonoBehaviour），无 Unity 生命周期依赖。

### 成员

| 成员 | 说明 |
| --- | --- |
| `EntityState currentState { get; private set; }` | 当前状态，外部只读 |
| `bool canChangeState` | 是否允许切换状态，默认 `false`，`Initialize` 时置 `true` |
| `event Action<EntityState> OnStateChanged` | 状态切换成功后触发，参数为新状态；用于后续调试与 UI 同步 |
| `void Initialize(EntityState startState)` | `startState == null` 时抛 `ArgumentNullException`；否则 `canChangeState = true`；赋值 `currentState`；调用 `Enter()` |
| `bool ChangeState(EntityState newState)` | 见下方行为契约 |
| `void UpdateActiveState()` | 调用 `currentState.Update()` |
| `void SwitchOffStateMachine()` | `canChangeState = false`，用于死亡等终态 |

### ChangeState 行为契约

按顺序判断，任一命中则返回 `false` 且不产生任何副作用：

1. `canChangeState == false`
2. `newState == null`
3. `newState == currentState`

否则执行：`currentState.Exit()` → `currentState = newState` → `currentState.Enter()` → 触发 `OnStateChanged`，返回 `true`。

`Initialize` 是唯一允许出现 `null` 的入口，且会立即抛 `ArgumentNullException`；因此 `currentState` 一旦赋值就不会为 `null`，`ChangeState` 与 `UpdateActiveState` 内部无需再做空判断。

## EntityState

`public abstract class EntityState`，非 MonoBehaviour。

### 字段

| 字段 | 可见性 | 说明 |
| --- | --- | --- |
| `stateMachine` | `protected readonly` | 由构造函数注入 |
| `animBoolName` | `protected readonly` | 对应 Animator 的 bool 参数名，可为 `null` |
| `anim` | `protected readonly` | 可为 `null` |
| `stateTimer` | `protected` | 通用倒计时，`Enter` 不重置，由具体状态自行赋值 |
| `triggerCalled` | `protected` | 动画事件触发标记，`Enter` 时重置为 `false` |

### 构造函数

```csharp
public EntityState(StateMachine stateMachine, string animBoolName = null, Animator anim = null)
```

`stateMachine` 为必需参数；`animBoolName` 与 `anim` 可省略。

### 生命周期

| 方法 | 行为 |
| --- | --- |
| `virtual Enter()` | `triggerCalled = false`；当 `anim != null` 且 `animBoolName` 非空时 `anim.SetBool(animBoolName, true)` |
| `virtual Update()` | `stateTimer -= Time.deltaTime`；随后调用 `UpdateAnimationParameters()` |
| `virtual Exit()` | 当 `anim != null` 且 `animBoolName` 非空时 `anim.SetBool(animBoolName, false)` |

### 其他方法

| 方法 | 行为 |
| --- | --- |
| `virtual UpdateAnimationParameters()` | 空钩子，留给子类每帧同步动画参数（如速度、朝向） |
| `void AnimationTrigger()` | `triggerCalled = true`；供 Animation Event 经 MonoBehaviour 转发调用 |
| `void ResetTimer()` | `stateTimer = 0f` |

`triggerCalled` 只由 `AnimationTrigger()` 置位、由 `Enter()` 复位；不使用后不自动清零，与参考实现一致。

## 设计取舍

- **Animator 可选**：无动画的纯逻辑状态（如 AI 决策、UI 状态）直接省略参数即可，不会抛空引用。
- **拒绝重复状态**：`ChangeState(sameState)` 返回 `false`，避免 `Exit`/`Enter` 被重复调用导致动画闪烁、计时器复位。
- **不重置 stateTimer**：`Enter` 保持参考实现语义，由每个具体状态显式设置自己的持续时间，避免基类猜测默认值。

## 验证

1. Unity 重新编译后，`read_console` 中无编译错误。
2. 可选自动化测试（本次不强制交付）：新建 `Assets/Tests/EditMode/` 与测试 asmdef，用假状态类覆盖以下用例：
   - `Initialize` 后 `currentState` 为起始状态且 `Enter` 被调用一次
   - `ChangeState` 依次调用旧状态 `Exit`、新状态 `Enter`
   - `ChangeState(null)`、`ChangeState(currentState)`、`SwitchOffStateMachine()` 之后返回 `false` 且状态不变
   - `anim == null` 时 `Enter` / `Exit` 不抛异常
   - `Update` 使 `stateTimer` 递减

## 验收标准

- 两个文件编译通过，`Assets/Scripts/StateMachine/` 下无其它新增文件。
- 任意继承 `EntityState` 的类只需实现构造函数与可选覆写，无需修改基类。
- 后续 `Enemy` 只需持有 `StateMachine` 实例并调用 `Initialize` / `UpdateActiveState` 即可驱动状态。
