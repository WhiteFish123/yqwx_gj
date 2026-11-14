using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.3f;
    public Vector3 offset = new Vector3(0, 0, -10f);

    [Header("画面补偿（只影响镜头，不影响游戏逻辑）")]
    [Tooltip("如果角色不在屏幕正中，用这个把镜头推回去。X 填负数 = 镜头往左推（画面内容往右移），Y 同理")]
    public Vector2 visualAdjust = Vector2.zero;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 wanted = target.position + offset + AdjustOffset();
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref velocity, smoothTime);
        }
    }

    //传送后调用：立刻贴到目标身上，并把平滑速度清零。
    //不这么做的话，相机会从旧位置一路 SmoothDamp "飞"过去，穿过整张地图。
    public void SnapToTarget()
    {
        if (target == null) return;

        transform.position = target.position + offset + AdjustOffset();
        velocity = Vector3.zero;
    }

    //把 Inspector 里的补偿值转成世界坐标偏移（z 不动，保证 2D 层的深度不变）
    private Vector3 AdjustOffset()
    {
        return new Vector3(visualAdjust.x, visualAdjust.y, 0f);
    }
}
