using UnityEngine;

[ExecuteInEditMode]
public class SnapToGrid : MonoBehaviour//编辑器工具：把物体吸附到 Grid 格子中心（仅在编辑模式下运行）
{
    public Grid grid;//目标Grid，留空则自动查找场景中的 Grid

    private void Update()
    {
        if (Application.isPlaying) return;//运行时不吸附

        if (grid == null)
            grid = FindFirstObjectByType<Grid>();
        if (grid == null) return;

        Vector3Int cell = grid.WorldToCell(transform.position);//世界坐标转格子坐标
        transform.position = grid.GetCellCenterWorld(cell);//对齐到格子中心
    }
}
