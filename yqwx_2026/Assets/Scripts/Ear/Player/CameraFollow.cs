using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.3f;
    public Vector3 offset =new Vector3(0, 0, -10f);

    private Vector3 velocity = Vector3.zero;
    
    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 wanted = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref velocity, smoothTime);
        }
    }

    //传送后调用：立刻贴到目标身上，并把平滑速度清零。
    //不这么做的话，相机会从旧位置一路 SmoothDamp "飞"过去，穿过整张地图。
    public void SnapToTarget()
    {
        if (target == null) return;

        transform.position = target.position + offset;
        velocity = Vector3.zero;
    }
}
