using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float smoothSpeed = 0.125f;

    void LateUpdate() // Dùng LateUpdate để cam đi sau nhân vật, tránh rung
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        // Lerp để camera lướt theo mượt mà
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}