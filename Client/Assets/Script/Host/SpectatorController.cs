using UnityEngine;

public class SpectatorController : MonoBehaviour
{
    public float panSpeed = 20f;   // Tốc độ di chuyển
    public float zoomSpeed = 5f;   // Tốc độ zoom
    public float minZoom = 5f;     // Zoom gần nhất
    public float maxZoom = 20f;    // Zoom xa nhất

    private Camera myCam;

    void Start()
    {
        Camera myCam = GetComponentInChildren<Camera>();
        if (myCam == null)
        {
            Debug.LogError("LỖI: Script này phải gắn vào GameObject có chứa Camera!");
        }
    }

    void Update()
    {
        // 1. Lấy tín hiệu từ bàn phím (Cả WASD và Mũi tên đều nhận)
        float h = Input.GetAxis("Horizontal"); // Trái/Phải (A/D, Mũi tên)
        float v = Input.GetAxis("Vertical");   // Lên/Xuống (W/S, Mũi tên)

        // Debug xem phím có ăn không
        if (h != 0 || v != 0)
        {
            // Debug.Log($"Đang bấm di chuyển: {h}, {v}");
        }

        // 2. Tính toán vị trí mới
        Vector3 pos = transform.position;
        pos.x += h * panSpeed * Time.deltaTime;
        pos.y += v * panSpeed * Time.deltaTime;
        transform.position = pos;

        // 3. Xử lý Zoom (Lăn chuột)
        if (myCam != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                myCam.orthographicSize -= scroll * zoomSpeed;
                myCam.orthographicSize = Mathf.Clamp(myCam.orthographicSize, minZoom, maxZoom);
            }
        }
    }
}