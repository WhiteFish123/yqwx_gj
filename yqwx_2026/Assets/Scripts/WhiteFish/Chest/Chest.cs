using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//可被抓取推拉的箱子：维护自身格子坐标、全局占用查询与逐格插值
public class Chest : MonoBehaviour
{
    [SerializeField] private Grid grid;//所在网格，留空自动查找

    private static readonly List<Chest> activeChests = new List<Chest>();

    private Vector2Int cell;//逻辑格子坐标

    public Vector2Int Cell => cell;
    public bool IsMoving { get; private set; }

    public static Chest At(Vector2Int target)//查询占用某格的箱子，无则返回 null
    {
        foreach (Chest chest in activeChests)
        {
            if (chest != null && chest.cell == target)
                return chest;
        }

        return null;
    }

    private void Awake()//先于 OnEnable 注册，保证进表时格子已正确
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        if (grid != null)
            cell = (Vector2Int)grid.WorldToCell(transform.position);
    }

    private void OnEnable()
    {
        if (!activeChests.Contains(this))
            activeChests.Add(this);
    }

    private void OnDisable()
    {
        activeChests.Remove(this);
    }

    public void MoveTo(Vector2Int target, float duration)//逻辑位置先行，视觉上插值跟上
    {
        cell = target;
        StartCoroutine(MoveRoutine(target, duration));
    }

    private IEnumerator MoveRoutine(Vector2Int target, float duration)
    {
        IsMoving = true;

        Vector3 start = transform.position;
        Vector3 end = grid.GetCellCenterWorld(new Vector3Int(target.x, target.y, 0));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        transform.position = end;
        IsMoving = false;
    }
}
