using UnityEngine;

public class SpectatorController : MonoBehaviour
{
    public float panSpeed = 20f; // Tốc độ lướt
    public float zoomSpeed = 2f; // Tốc độ zoom

    void Update()
    {
        // --- 1. Di chuyển Camera bằng WASD hoặc Mũi tên ---
        Vector3 pos = transform.position;

        if (Input.GetKey("w") || Input.GetKey(KeyCode.UpArrow))
        {
            pos.y += panSpeed * Time.deltaTime;
        }
        if (Input.GetKey("s") || Input.GetKey(KeyCode.DownArrow))
        {
            pos.y -= panSpeed * Time.deltaTime;
        }
        if (Input.GetKey("d") || Input.GetKey(KeyCode.RightArrow))
        {
            pos.x += panSpeed * Time.deltaTime;
        }
        if (Input.GetKey("a") || Input.GetKey(KeyCode.LeftArrow))
        {
            pos.x -= panSpeed * Time.deltaTime;
        }

        // --- 2. Zoom ra vào bằng con lăn chuột ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Camera cam = GetComponent<Camera>();
        cam.orthographicSize -= scroll * zoomSpeed * 100f * Time.deltaTime;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, 5f, 20f); // Giới hạn zoom

        transform.position = pos;
    }
}