using System.Collections.Generic;
using UnityEngine;

//敌人注册表：避免每帧 FindObjectsByType<Enemy>() 分配数组。
//敌人数量少，注册表本身就用 List，但查询时不分配。
public static class EnemyRegistry
{
    private static readonly List<Enemy> all = new List<Enemy>();

    //当前场景里所有活着的敌人（只读遍历用）
    public static IReadOnlyList<Enemy> All => all;

    //玩家引用缓存：FindGameObjectWithTag 每次都会走 native 查找
    public static Transform Player { get; private set; }

    //刷新玩家引用（玩家换场景/重生时调用一次即可）
    public static void RefreshPlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        Player = go != null ? go.transform : null;
    }

    public static void Register(Enemy enemy)
    {
        if (enemy == null || all.Contains(enemy))
            return;

        all.Add(enemy);
    }

    public static void Unregister(Enemy enemy)
    {
        if (enemy == null)
            return;

        all.Remove(enemy);
    }

    //该格是否被其他活着的敌人占着
    public static bool IsOccupiedByOther(Enemy self, Vector3Int cell)
    {
        for (int i = 0; i < all.Count; i++)
        {
            Enemy other = all[i];

            if (other == null || other == self || other.isDead)
                continue;

            if (other.WorldToCell(other.transform.position) == cell)
                return true;
        }

        return false;
    }

    //该格是否被玩家占着
    public static bool IsOccupiedByPlayer(Vector3Int cell, System.Func<Vector3, Vector3Int> worldToCell)
    {
        if (Player == null)
            RefreshPlayer();

        if (Player == null || worldToCell == null)
            return false;

        return worldToCell(Player.position) == cell;
    }
}
