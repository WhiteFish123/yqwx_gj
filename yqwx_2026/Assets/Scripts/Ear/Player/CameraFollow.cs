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
}
