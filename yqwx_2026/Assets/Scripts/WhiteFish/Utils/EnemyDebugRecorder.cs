using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

//临时诊断：记录敌人状态变化，便于复现问题时回看（Play 模式专用）
public class EnemyDebugRecorder : MonoBehaviour
{
    private static readonly List<string> entries = new List<string>();//记录内容
    private const int maxEntries = 2000;//最多保留条数

    [SerializeField] private float sampleInterval = 0.1f;//采样间隔

    private float nextSample;
    private string lastSignature;

    private static readonly FieldInfo pathField = typeof(Enemy_PathMoveState)
        .GetField("path", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo indexField = typeof(Enemy_PathMoveState)
        .GetField("index", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Clear()
    {
        entries.Clear();
    }

    public static string Dump(int maxLines = 200)
    {
        if (entries.Count == 0)
            return "（没有记录）";

        int start = Mathf.Max(0, entries.Count - maxLines);
        StringBuilder sb = new StringBuilder();
        sb.Append("共 ").Append(entries.Count).Append(" 条，显示最后 ").Append(entries.Count - start).Append(" 条：\n");

        for (int i = start; i < entries.Count; i++)
            sb.Append(entries[i]).Append("\n");

        return sb.ToString();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSample)
            return;

        nextSample = Time.unscaledTime + sampleInterval;

        string signature = BuildSignature();
        if (signature == lastSignature)
            return;

        lastSignature = signature;

        if (entries.Count >= maxEntries)
            entries.RemoveAt(0);

        entries.Add("t=" + Time.time.ToString("F2") + " " + signature);
    }

    private string BuildSignature()//只在"敌人跨格 / 状态 / 朝向 / 玩家格"变化时才记录
    {
        StringBuilder sb = new StringBuilder();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        sb.Append("[玩家 ").Append(player == null ? "无" : player.transform.position.ToString("F1")).Append("] ");

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            object state = enemy.stateMachine == null ? null : enemy.stateMachine.currentState;
            string stateName = state == null ? "null" : state.GetType().Name;

            sb.Append(enemy.name)
              .Append(" cell").Append(enemy.WorldToCell(enemy.transform.position).ToString())
              .Append(" ").Append(stateName)
              .Append(" face").Append(enemy.facing.ToString())
              .Append(" vel").Append(enemy.rb == null ? "null" : enemy.rb.linearVelocity.ToString("F1"))
              .Append(" see=").Append(enemy.CanSeePlayer() ? "Y" : "N");

            if (state is Enemy_PathMoveState pathMove)
            {
                List<Vector3Int> path = pathField == null ? null : pathField.GetValue(pathMove) as List<Vector3Int>;
                object index = indexField == null ? null : indexField.GetValue(pathMove);
                sb.Append(" idx=").Append(index);
                sb.Append(" path=");
                sb.Append(path == null ? "null" : path.Count + "[" + string.Join("", path) + "]");
            }

            sb.Append(" | ");
        }

        return sb.ToString();
    }
}
