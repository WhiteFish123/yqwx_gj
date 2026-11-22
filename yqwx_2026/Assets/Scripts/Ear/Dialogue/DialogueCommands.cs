// 把 Yarn 剧本里的 <<命令>> 接到游戏逻辑上。
// 挂在 DialogueSystem 这个物体上（和 DialogueRunner 同一物体）就会自动注册。
//
// 目前支持：
//   <<apply_damage 1>>   玩家扣 1 点血（走 PlayerHealth，会触发无敌帧、飘红、血量 UI）
//   <<heal_player 1>>    玩家回 1 点血
//   <<drive_fx_start>>   开场驾驶特写的占位（现在只打日志）
//   <<boss_fight_start>> BOSS 战的占位（现在只打日志，等 BOSS 逻辑做好再填）
//
// 两个坑（都踩过）：
// 1. 这版 Yarn 只有 AddCommandHandler(string, Delegate)，没有泛型，Delegate 必须显式转。
// 2. Yarn 的命令表是 **static 的全局表**（ActionInvoker 里的静态 Actions 实例），
//    它跨场景、跨 play 会话都不清空。所以同一台 / 不同 runner 再注册一次同名命令
//    就会报 “a command by this name has already been registered”。
//    因此这里用静态集合按「命令名」去重，全局只注册一次；处理函数也全是静态的，
//    不捕捉任何场景对象，换场景后老注册项照样能用。
using UnityEngine;

public class DialogueCommands : MonoBehaviour
{
    [Header("引用（留空自动找）")]
    public Yarn.Unity.DialogueRunner runner;

    private static bool registered;
    private static PlayerHealth cachedHealth;

    private void Awake()
    {
        if (runner == null)
            runner = GetComponent<Yarn.Unity.DialogueRunner>();
    }

    private void Start()
    {
        Register(runner);
    }

    // 全局只做一次：Yarn 的命令表是静态的
    public static void Register(Yarn.Unity.DialogueRunner runner)
    {
        if (registered || runner == null)
            return;

        registered = true;

        // 静态方法组：Yarn 判断“实例命令”看方法名里有没有 '<'、以及方法本身是否 static，
        // 静态方法只吃掉“命令名”一项参数，正好匹配 <<apply_damage 1>> 这种两段式。
        AddOrReplace(runner, "apply_damage", (System.Action<string>)(ApplyDamage));
        AddOrReplace(runner, "heal_player", (System.Action<string>)(HealPlayer));
        AddOrReplace(runner, "drive_fx_start", (System.Action)(DriveFxStart));
        AddOrReplace(runner, "boss_fight_start", (System.Action)(BossFightStart));
    }

    // Yarn 的命令表跨场景、跨 play 会话都不清空（ActionInvoker 里是静态实例），
    // 上一次会话注册过的同名命令会一直留在里面。这里先把它摘掉再注册，
    // 既能换成本次会话的处理函数，也不会触发 “already registered” 报错。
    private static void AddOrReplace(Yarn.Unity.DialogueRunner runner, string commandName, System.Delegate handler)
    {
        try
        {
            object actions = GetActionsInstance();

            if (actions != null && CommandExists(actions, commandName))
                RemoveCommand(actions, commandName);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("<<对话命令>> 清理旧命令 " + commandName + " 失败：" + e.Message);
        }

        runner.AddCommandHandler(commandName, handler);
    }

    private static object GetActionsInstance()
    {
        if (actionsField == null)
        {
            actionsField = typeof(Yarn.Unity.ActionInvoker).GetField(
                "runtimeDelegateActionInvoker",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        }

        return actionsField != null ? actionsField.GetValue(null) : null;
    }

    private static bool CommandExists(object actions, string commandName)
    {
        if (commandsField == null)
        {
            commandsField = actions.GetType().GetField(
                "_commands",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        if (commandsField == null)
            return false;

        var map = commandsField.GetValue(actions) as System.Collections.IDictionary;
        return map != null && map.Contains(commandName);
    }

    private static void RemoveCommand(object actions, string commandName)
    {
        var method = actions.GetType().GetMethod("RemoveCommandHandler", new[] { typeof(string) });

        if (method != null)
            method.Invoke(actions, new object[] { commandName });
    }

    private static System.Reflection.FieldInfo actionsField;
    private static System.Reflection.FieldInfo commandsField;

    private static PlayerHealth Health()
    {
        // 换场景后老引用会变成“假 null”，所以每次检查一遍
        if (cachedHealth == null)
            cachedHealth = Object.FindFirstObjectByType<PlayerHealth>();

        return cachedHealth;
    }

    private static int ParseAmount(string amount)
    {
        int value = 1;

        if (!string.IsNullOrEmpty(amount) && int.TryParse(amount.Trim(), out int parsed))
            value = parsed;

        return Mathf.Max(1, value);
    }

    private static void ApplyDamage(string amount)
    {
        PlayerHealth h = Health();

        if (h == null)
        {
            Debug.LogWarning("<<apply_damage>> 找不到 PlayerHealth");
            return;
        }

        h.Hurt(ParseAmount(amount));
    }

    private static void HealPlayer(string amount)
    {
        PlayerHealth h = Health();

        if (h == null)
            return;

        h.Heal(ParseAmount(amount));
    }

    private static void DriveFxStart()
    {
        // TODO: 接开场驾驶特写（车灯 / 雨刷）
        Debug.Log("[对话命令] drive_fx_start：驾驶特写还没接");
    }

    private static void BossFightStart()
    {
        // TODO: 接 BOSS 战开始（叫醒爱丽丝 / 关掉门 / 播音乐）
        Debug.Log("[对话命令] boss_fight_start：BOSS 战还没接");
    }
}
