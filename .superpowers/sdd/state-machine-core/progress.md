# SDD ledger — plan: docs/superpowers/plans/2026-11-14-state-machine-core.md

Pre-flight: no shared interfaces（任务 1 只创建文件，任务 2 覆写同两个文件，任务 3 只读反射）
Ruling: 直接在 main 分支实现 — 用户明确要求"直接在本地文件夹开始写脚本"，不建 worktree — 若错：改动未提交，可随时丢弃
Ruling: 全程不执行 git commit — 用户明确要求不要 commit — 若错：成果仅存在于工作区

Task 1: complete（红阶段：run_tests EditMode → total 0；read_console 报 CS0115 三条，属预期的"成员不存在"类错误，无 asmdef 引用错误）
Task 2: Ruling: 计划写"7 个测试全部通过"，实际测试文件有 8 个用例 — 按实际 8 个验收 — 若错：多一个用例，无行为影响
Task 2: complete（run_tests EditMode → 8/8 passed；read_console error+warning 无 CS 错误）
Task 3: Ruling: unity_reflect get_type("StateMachine") 返回歧义（Unity.VisualScripting.StateMachine、UnityEditorInternal.StateMachine）— 改用 execute_code 反射验证程序集、成员、抽象性与可实例化 — 若错：验证方式等价，无行为影响
Task 3: complete（execute_code → asm=WhiteFish.StateMachine，8 个公共成员齐全，EntityState abstract=True、1 个构造，可 new，canChangeState 初始 false）

Final review: subagent (general, 全新上下文) — 4 Important / 8 Minor
Final: fixed Initialize 重复调用会覆盖状态并解除锁定 — Initialize_Twice_ThrowsAndKeepsLockedState RED→GREEN, suite 9/9
Final: fixed 断言过弱（SwitchOff / ChangeState(null) 未验副作用与事件）— 补 log 与 raised 断言, suite 9/9
Final: Ruling: 拒绝 Important-2（给 Enter/Exit/OnStateChanged 包 try-catch 吞异常）— 吞掉生命周期异常会隐藏编程错误，且当前无事件订阅方 — 若错：订阅方可能漏收状态变化
Final: Ruling: Important-3 降级为 Minor（anim 非空 + 空 animBoolName 的测试）— 要真断言 Animator bool 必须现场创建 AnimatorController，成本高于风险 — 若错：空 animBoolName 时 SetBool 会刷警告
Final: 文档同步：spec 中"属于默认 Assembly-CSharp"改为独立 asmdef 说明
Final: minor (deferred): UpdateActiveState 未 Initialize 即调用会 NRE（规格允许，仅缺注释）
Final: minor (deferred): EntityState 构造函数不校验 stateMachine == null
Final: minor (deferred): UpdateAnimationParameters / AnimationTrigger / ResetTimer 无测试
Final: minor (deferred): OnStateChanged 事件泄漏（发布者短生命周期，风险低）
Final: minor (deferred): XML 注释"目标为 null"沿用 Enemy 语汇
