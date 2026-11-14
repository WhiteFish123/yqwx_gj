# SDD ledger — plan: docs/superpowers/plans/2026-11-14-enemy-death.md

Ruling: 不执行 git commit — 沿用用户此前"不要 commit"的指示，本次亦未授权提交 — 如果错了的代价：改动留在工作区，需用户自行提交。

Pre-flight: 任务 2 产出 `Enemy.currentHealth` / `isDead` / `deadState` / `TakeDamage` / `DestroyGameObjectWithDelay` 与 `Enemy_DeadState(Enemy, StateMachine, string, float)`，任务 1 的测试与任务 3 的 Play 验证均按此签名消费，无冲突。

Task 1: complete (red) — 21 tests total，7 条新增用例全部失败且原因正确（hp 为 0、状态仍是 Idle、碰撞仍开着），原有 14 条通过。

Task 2: Ruling: `Destroy` 在编辑模式会输出 error「Destroy may not be called from edit mode」，Unity Test Framework 判定为测试失败 — 给 `DestroyGameObjectWithDelay` 加 `if (!Application.isPlaying) return;`，Play 模式行为不变 — 如果错了的代价：在编辑器（非 Play）手动调用 `TakeDamage` 了结敌人时看不到销毁，尸体留在场景里。

Task 2: complete — tests: unityMCP_run_tests EditMode → 21/21 passed, 0 failed。

Task 3: Ruling: 首次在同一个 execute_code 调用里读取 clip 得到 `Slime_Idle` — `anim.Play` 在当帧尚未生效，属于读取时机而非实现缺陷；改为 `TakeDamage` 后等待 0.6 秒再读，得到 `Slime_Dead` — 如果错了的代价：无，已用等待后的读取证实。

Task 3: complete — Play 模式：hp=3 → 1 点后 hp=2 dead=False → 再 2 点后 hp=0 dead=True state=Enemy_DeadState colEnabled=False clip=Slime_Dead normalizedTime=1.41（loop=false 已停在末帧），2 秒后 GameObject.Find 返回 null，Console 0 条 error/warning，已退出 Play 模式。

Final review: 分派 general 子代理审查，2 条 Important + 8 条 Minor，无 Critical。

Final: fixed `Die()` 先置 `isDead` 再切状态、忽略返回值，导致「状态机未初始化」与「deadState 未配置」两种情况下敌人变成血量 0、杀不死、不消失的僵尸 — 新增两测试 `TakeDamage_BeforeInitialize_DoesNotStayDead`、`TakeDamage_WithoutDeadState_LogsErrorAndDoesNotDie`，RED（Expected False But was True ×2）→ GREEN，实现改为 null 报错 + 切换失败回滚 isDead；suite 23/23。

Final: fixed 规格未把 Controller / Scene / anim / sprite meta 的资源改动纳入交付范围 — 已补「资源前置」一节与验收对照 — 若不补，只交付 .cs 会得到「测试绿、实机无死亡动画」的假成功。

Final: Ruling: 8 条 Minor 一律延后，不进入修复轮 — 它们不改变当前可达行为（其中「Initialize 前受伤」一条已被上面的修复连测试一起覆盖）— 如果错了的代价：留下若干可维护性缺口，见最终消息的延后清单。

Final: minor (deferred): `anim.Play` 的 clip 名缺少 EditMode 断言（Controller 状态重命名会静默丢动画）。
Final: minor (deferred): `TakeDamage` / `Die` 的 virtual 覆写契约未在注释声明（覆写不调 base 会绕过保护）。
Final: minor (deferred): `DestroyGameObjectWithDelay` 的注释易被读成「编辑模式下什么都不做」，实际其余死亡副作用仍生效。
Final: minor (deferred): `Enemy_DeadState` 类注释未提编辑模式不销毁。
Final: minor (deferred): `damage <= 0f` 挡不住 NaN，会使血量变 NaN 且永不死亡。
Final: minor (deferred): clamp 归零那行未被测试覆盖（`TakeDamage(3f)` 恰好不产生负血量）。
Final: minor (deferred): `deadState` 是 public 字段，可被外部置 null 或替换。
Final: minor (deferred): 外部误调 `SwitchOffStateMachine` 会把存活敌人永久钉在当前状态。
