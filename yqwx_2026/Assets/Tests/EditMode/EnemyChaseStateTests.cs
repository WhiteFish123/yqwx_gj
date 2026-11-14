using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EnemyChaseStateTests
{
    private GameObject root;
    private GameObject player;
    private GameObject floor;
    private GameObject scenePlayer;
    private Grid grid;
    private Enemy_Slime slime;
    private Animator animator;
    private readonly int obstacleLayer = LayerMask.NameToLayer("Obstacle");

    [SetUp]
    public void SetUp()
    {
        if (obstacleLayer < 0)
            Assert.Ignore("Obstacle Layer 未创建");

        //屏蔽场景里已有的 Player，否则 FindGameObjectWithTag 会串到场景对象
        GameObject existing = GameObject.Find("Player_Test");
        if (existing != null)
        {
            scenePlayer = existing;
            scenePlayer.SetActive(false);
        }

        floor = new GameObject("ChaseTestRoot");
        grid = floor.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        root = new GameObject("Enemy_Slime_Test");
        root.transform.SetParent(floor.transform);
        root.transform.position = grid.GetCellCenterWorld(Vector3Int.zero);
        root.AddComponent<CapsuleCollider2D>();
        root.AddComponent<Rigidbody2D>();

        GameObject animation = new GameObject("Animation");
        animation.transform.SetParent(root.transform);
        SpriteRenderer renderer = animation.AddComponent<SpriteRenderer>();
        renderer.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Test/Slime/Individual Sprites/slime-idle-0.png");
        animator = animation.AddComponent<Animator>();
        animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/AnimatorControllers/Enemy_Slime.controller");

        slime = root.AddComponent<Enemy_Slime>();
    }

    [TearDown]
    public void TearDown()
    {
        if (player != null)
            Object.DestroyImmediate(player);

        if (floor != null)
            Object.DestroyImmediate(floor);

        if (scenePlayer != null)
            scenePlayer.SetActive(true);
    }

    private void CreatePlayerAt(Vector2Int cell)
    {
        if (player == null)
            player = new GameObject("Player_Test");

        player.tag = "Player";
        player.transform.position = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    private static void InvokeLifecycle(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (info != null)
            info.Invoke(target, null);
    }

    private void StartSlime()
    {
        InvokeLifecycle(slime, "Awake");
        InvokeLifecycle(slime, "Start");
    }

    private void SetStateTimer(object state, float value)
    {
        FieldInfo timer = typeof(EntityState).GetField("stateTimer",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.IsNotNull(timer, "EntityState 缺少 stateTimer 字段");
        timer.SetValue(state, value);
    }

    [Test]
    public void CanSeePlayer_WithinView_ReturnsTrue()
    {
        StartSlime();
        CreatePlayerAt(new Vector2Int(2, 0));

        Assert.IsTrue(slime.CanSeePlayer());
    }

    [Test]
    public void CanSeePlayer_Behind_ReturnsFalse()
    {
        StartSlime();
        CreatePlayerAt(new Vector2Int(-2, 0));

        Assert.IsFalse(slime.CanSeePlayer(), "默认朝右，背后看不到");
    }

    [Test]
    public void IsOnPatrolRoute_DetectsCurrentCell()
    {
        StartSlime();
        slime.patrolPoints = new List<Vector3>
        {
            grid.GetCellCenterWorld(Vector3Int.zero),
            grid.GetCellCenterWorld(new Vector3Int(1, 0, 0))
        };

        Assert.IsTrue(slime.IsOnPatrolRoute());
    }

    [Test]
    public void FindClosestPatrolCell_ReturnsNearestPoint()
    {
        StartSlime();
        slime.patrolPoints = new List<Vector3>
        {
            grid.GetCellCenterWorld(new Vector3Int(5, 0, 0)),
            grid.GetCellCenterWorld(new Vector3Int(6, 0, 0))
        };

        Vector3Int? cell = slime.FindClosestPatrolCell();

        Assert.IsNotNull(cell);
        Assert.AreEqual(new Vector3Int(5, 0, 0), cell.Value);
    }

    [Test]
    public void SetFacing_UpdatesFacingAndIgnoresZero()
    {
        StartSlime();

        slime.SetFacing(Vector2Int.left);
        Assert.AreEqual(Vector2Int.left, slime.facing);

        slime.SetFacing(Vector2Int.zero);
        Assert.AreEqual(Vector2Int.left, slime.facing, "零向量应被忽略");
    }

    [Test]
    public void Move_SeesPlayer_SwitchesToChase()
    {
        StartSlime();
        CreatePlayerAt(new Vector2Int(2, 0));

        slime.SetFacing(Vector2Int.right);
        slime.moveState.Update();

        Assert.AreSame(slime.chaseState, slime.stateMachine.currentState);
    }

    [Test]
    public void Chase_LosesPlayer_SwitchesToIdle()
    {
        StartSlime();
        CreatePlayerAt(new Vector2Int(2, 0));
        slime.stateMachine.ChangeState(slime.chaseState);

        player.transform.position = grid.GetCellCenterWorld(new Vector3Int(-5, 0, 0));
        slime.chaseState.Update();

        Assert.AreSame(slime.idleState, slime.stateMachine.currentState);
    }

    [Test]
    public void Chase_TargetsCellAdjacentToPlayer()
    {
        StartSlime();
        CreatePlayerAt(new Vector2Int(4, 0));
        slime.stateMachine.ChangeState(slime.chaseState);

        MethodInfo getTarget = typeof(Enemy_ChaseState).GetMethod("GetTargetCell",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(getTarget, "Enemy_ChaseState 缺少 GetTargetCell");

        object result = getTarget.Invoke(slime.chaseState, null);
        Assert.IsNotNull(result, "应能算出追逐目标格");

        Vector3Int target = ((Vector3Int?)result).Value;
        Vector3Int playerCell = new Vector3Int(4, 0, 0);
        int distance = Mathf.Abs(target.x - playerCell.x) + Mathf.Abs(target.y - playerCell.y);

        Assert.AreEqual(1, distance, "目标必须是玩家四向相邻格，不能是玩家格本身");
    }

    [Test]
    public void Idle_EndsOffRoute_EntersReturn()
    {
        StartSlime();
        slime.patrolPoints = new List<Vector3>
        {
            grid.GetCellCenterWorld(new Vector3Int(8, 0, 0)),
            grid.GetCellCenterWorld(new Vector3Int(9, 0, 0))
        };

        root.transform.position = grid.GetCellCenterWorld(new Vector3Int(-3, 0, 0));
        slime.stateMachine.ChangeState(slime.idleState);

        SetStateTimer(slime.idleState, -1f);
        slime.idleState.Update();

        Assert.AreSame(slime.returnState, slime.stateMachine.currentState);
    }

    [Test]
    public void Idle_EndsOnRoute_EntersMove()
    {
        StartSlime();
        slime.patrolPoints = new List<Vector3>
        {
            grid.GetCellCenterWorld(Vector3Int.zero),
            grid.GetCellCenterWorld(new Vector3Int(1, 0, 0))
        };

        slime.stateMachine.ChangeState(slime.idleState);

        SetStateTimer(slime.idleState, -1f);
        slime.idleState.Update();

        Assert.AreSame(slime.moveState, slime.stateMachine.currentState);
    }

    [Test]
    public void Return_Arrived_EntersMove()
    {
        StartSlime();
        slime.patrolPoints = new List<Vector3>
        {
            grid.GetCellCenterWorld(new Vector3Int(3, 0, 0)),
            grid.GetCellCenterWorld(new Vector3Int(4, 0, 0))
        };

        root.transform.position = grid.GetCellCenterWorld(new Vector3Int(-1, 0, 0));
        slime.stateMachine.ChangeState(slime.returnState);

        MethodInfo arrived = typeof(Enemy_ReturnState).GetMethod("OnArrived",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(arrived, "Enemy_ReturnState 缺少 OnArrived");
        arrived.Invoke(slime.returnState, null);

        Assert.AreSame(slime.moveState, slime.stateMachine.currentState);
    }
}
