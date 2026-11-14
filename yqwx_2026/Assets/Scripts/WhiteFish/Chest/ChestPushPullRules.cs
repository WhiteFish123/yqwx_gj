using System;
using UnityEngine;

//箱子推拉规则：纯逻辑，不依赖场景对象，便于单测
public enum ChestMoveKind
{
    None,
    Push,
    Pull,
}

public static class ChestPushPullRules
{
    public static bool CanGrab(Vector2Int playerCell, Vector2Int facing, bool chestInFront)//只有正前方有箱子才能抓
    {
        return facing != Vector2Int.zero && chestInFront;
    }

    public static ChestMoveKind Resolve(Vector2Int facing, Vector2Int input)//朝向方向推，反方向拉，其余无效
    {
        if (input == Vector2Int.zero || facing == Vector2Int.zero)
            return ChestMoveKind.None;

        if (input == facing)
            return ChestMoveKind.Push;

        if (input == -facing)
            return ChestMoveKind.Pull;

        return ChestMoveKind.None;
    }

    public static bool TryComputeTargets(Vector2Int playerCell, Vector2Int chestCell, Vector2Int facing,
        ChestMoveKind kind, out Vector2Int playerTarget, out Vector2Int chestTarget)//计算玩家与箱子各自的目标格
    {
        playerTarget = playerCell;
        chestTarget = chestCell;

        if (facing == Vector2Int.zero || chestCell != playerCell + facing)
            return false;

        switch (kind)
        {
            case ChestMoveKind.Push:
                playerTarget = chestCell;
                chestTarget = chestCell + facing;
                return true;

            case ChestMoveKind.Pull:
                playerTarget = playerCell - facing;
                chestTarget = playerCell;
                return true;

            default:
                return false;
        }
    }

    public static bool CanExecute(Vector2Int playerCell, Vector2Int chestCell, Vector2Int facing,
        ChestMoveKind kind, Func<Vector2Int, bool> isBlocked, Func<Vector2Int, bool> hasOtherChest)//目标格必须可走且无其他箱子
    {
        if (!TryComputeTargets(playerCell, chestCell, facing, kind, out Vector2Int playerTarget, out Vector2Int chestTarget))
            return false;

        Vector2Int check = kind == ChestMoveKind.Push ? chestTarget : playerTarget;
        return !isBlocked(check) && !hasOtherChest(check);
    }
}
