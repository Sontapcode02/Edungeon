using UnityEngine;

public class SpectatorController : MonoBehaviour
{
    public float panSpeed = 20f;   // Tốc độ di chuyển
    public float zoomSpeed = 5f;   // Tốc độ zoom
    public float minZoom = 5f;     // Zoom gần nhất
    public float maxZoom = 20f;    // Zoom xa nhất

    [Header("Map Limits")]
    public bool useMapLimit = true;
    public Vector2 minBounds = new Vector2(-50, -50);
    public Vector2 maxBounds = new Vector2(50, 50);

    private Camera myCam;

    void Start()
    {
        myCam = GetComponentInChildren<Camera>(); // Fixed: removed local declaration shadowing field
        if (myCam == null)
        {
            Debug.LogError("LỖI: Script này phải gắn vào GameObject có chứa Camera!");
        }

        // Safety check: if spawned outside valid bounds, move to center
        if (useMapLimit)
        {
            Debug.Log($"📷 SpectatorController: Init Pos: {transform.position}. Bounds: {minBounds} to {maxBounds}");
            float clampedX = Mathf.Clamp(transform.position.x, minBounds.x, maxBounds.x);
            float clampedY = Mathf.Clamp(transform.position.y, minBounds.y, maxBounds.y);

            if (transform.position.x != clampedX || transform.position.y != clampedY)
            {
                Vector3 newPos = new Vector3((minBounds.x + maxBounds.x) / 2, (minBounds.y + maxBounds.y) / 2, transform.position.z);
                Debug.LogWarning($"⚠️ SpectatorController: Out of bounds! Moving to center: {newPos}");
                transform.position = newPos;
            }
        }
        else
        {
            Debug.Log("📷 SpectatorController: Map Limit is OFF.");
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

        if (useMapLimit)
        {
            pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
            pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
        }

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