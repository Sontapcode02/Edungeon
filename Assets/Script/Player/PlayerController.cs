using UnityEngine;
using Newtonsoft.Json;
using Cinemachine;
using System.Collections.Generic;
public class PlayerController : MonoBehaviour
{
    public string PlayerId;
    public bool IsLocal = false;

    [Header("Settings")]
    public float moveSpeed = 5f;
    public float smoothTime = 10f;
    [Header("Network Smoothing")]
    public float interpolationDelay = 0.1f;
    private List<PositionSnapshot> serverSnapshots = new List<PositionSnapshot>();

    private Animator anim;
    private Rigidbody2D rb;
    private Vector3 lastPos;
    private float lastSendTime;
    void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        
        if (rb == null)
        {
            Debug.LogError($"[ERROR] Prefab '{gameObject.name}' không có Rigidbody2D! Hãy thêm vào prefab.");
        }
    }

    void Start()
    {
        lastPos = transform.position;
    }
    public void OnServerDataReceived(Vector3 newPos)
    {
        // Lưu vị trí kèm thời gian nhận được vào danh sách
        serverSnapshots.Add(new PositionSnapshot(newPos, Time.time));

        // Dọn dẹp bộ nhớ: Xóa các điểm quá cũ (cũ hơn 1 giây) để đỡ nặng máy
        if (serverSnapshots.Count > 20)
        {
            serverSnapshots.RemoveAt(0);
        }
    }
    void Update()
    {
        // 1. Xử lý Animation (Dùng logic Speed = 0 đại ca đã duyệt)
        UpdateAnimation();

        // 2. Nếu là mình thì đi bằng phím (Code cũ)
        if (IsLocal) return;

        // 3. --- LOGIC DI CHUYỂN MƯỢT (INTERPOLATION) ---
        // Thời gian chúng ta muốn hiển thị (Quá khứ)
        float renderTime = Time.time - interpolationDelay;

        // Cần ít nhất 2 điểm dữ liệu để nội suy
        if (serverSnapshots.Count >= 2)
        {
            // Tìm 2 điểm bao quanh thời gian renderTime
            // Điểm A (cũ hơn renderTime) và Điểm B (mới hơn renderTime)
            PositionSnapshot snapshotA = serverSnapshots[0];
            PositionSnapshot snapshotB = serverSnapshots[0];

            // Duyệt ngược từ cuối về để tìm điểm phù hợp nhanh hơn
            for (int i = serverSnapshots.Count - 1; i >= 1; i--)
            {
                if (serverSnapshots[i].timestamp <= renderTime)
                {
                    // Đã tìm thấy điểm bắt đầu
                    snapshotA = serverSnapshots[i];
                    // Điểm kết thúc là điểm ngay sau nó
                    if (i + 1 < serverSnapshots.Count)
                        snapshotB = serverSnapshots[i + 1];
                    else
                        snapshotB = serverSnapshots[i]; // Hết dữ liệu thì đứng yên

                    break;
                }
            }

            // Tính toán tỷ lệ phần trăm (0.0 -> 1.0) giữa A và B
            // Ví dụ: A lúc 10s, B lúc 12s. RenderTime là 11s -> Tỷ lệ là 0.5
            float timeInterval = snapshotB.timestamp - snapshotA.timestamp;

            if (timeInterval > 0.0001f)
            {
                float t = (renderTime - snapshotA.timestamp) / timeInterval;

                // Di chuyển nhân vật
                transform.position = Vector3.Lerp(snapshotA.position, snapshotB.position, t);
            }
            else
            {
                transform.position = snapshotB.position;
            }
        }
        else if (serverSnapshots.Count == 1)
        {
            // Nếu mới có 1 điểm dữ liệu thì dịch chuyển tới đó luôn
            transform.position = Vector3.Lerp(transform.position, serverSnapshots[0].position, Time.deltaTime * 10f);
        }
    }

    void FixedUpdate()
    {
        // Di chuyển vật lý phải ở FixedUpdate
        if (IsLocal)
        {
            HandleInputPhysics();
        }
    }

    void HandleInputPhysics()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 movement = new Vector2(h, v).normalized;

        // Di chuyển bằng Rigidbody để không đi xuyên tường
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);

        // Gửi vị trí lên Server
        if (movement != Vector2.zero)
        {
            SendPosition();
        }
    }

    private bool isFirstUpdate = true; 
    public void SetNewPosition(Vector3 newPos)
    {
        if (isFirstUpdate)
        {
            // Lần đầu tiên cập nhật vị trí -> Teleport luôn cho đỡ chạy animation
            transform.position = newPos;
            lastPos = newPos;
            isFirstUpdate = false;
        }
        else
        {
            // Những lần sau thì mới đặt mục tiêu để Update nó Lerp từ từ
        }
    }

    // Trong PlayerController.cs

    void UpdateAnimation()
    {
        if (anim == null) return;

        float moveAmount = 0;
        Vector2 dir = Vector2.zero;

        // --- BƯỚC 1: TÍNH TOÁN ---
        if (IsLocal)
        {
            // Lấy input hiện tại
            float rawX = Input.GetAxisRaw("Horizontal");
            float rawY = Input.GetAxisRaw("Vertical");
            Vector2 inputDir = new Vector2(rawX, rawY);

            if (inputDir.sqrMagnitude > 0.01f)
            {
                moveAmount = 1f;
                dir = inputDir.normalized; // Lấy hướng đi
            }
        }
        else
        {
            // Code cho nhân vật Online
            float dist = Vector3.Distance(transform.position, lastPos);
            if (dist > 0.02f)
            {
                moveAmount = dist / Time.deltaTime;
                dir = (transform.position - lastPos).normalized;
            }
            lastPos = transform.position;
        }

        // --- BƯỚC 2: CẬP NHẬT ANIMATOR (FIX PAUSE) ---

        if (moveAmount > 0.01f)
        {
            // 1. Cập nhật hướng (InputX, InputY)
            anim.SetFloat("InputX", dir.x);
            anim.SetFloat("InputY", dir.y);

            // 2. [QUAN TRỌNG] Cho Animation CHẠY
            anim.speed = 1f;
        }
        else
        {
            anim.speed = 0f;
        }
    }

    public void Initialize(string id, bool local)
    {
        PlayerId = id;
        IsLocal = local;
        serverSnapshots.Clear();
        serverSnapshots.Add(new PositionSnapshot(transform.position, Time.time));
        Debug.Log($"[DEBUG] Initialize chạy cho ID: {id} | IsLocal: {local}");
        if (anim != null)
        {
            anim.SetFloat("InputX", 0);
            anim.SetFloat("InputY", 0);
            anim.SetBool("IsMoving", false);
        }
        lastPos = transform.position;
        if (IsLocal)
        {
            var vcam = FindObjectOfType<CinemachineVirtualCamera>();

            if (vcam != null)
            {
                vcam.Follow = transform;

                Debug.Log("Cinemachine đã nhận mục tiêu: " + id);
            }
            else
            {
                Debug.LogError("LỖI: Không tìm thấy CM vcam1 trong Scene!");
            }
            if (!local && GetComponent<Rigidbody2D>() != null)
            {
                GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }
    void SendPosition()
    {
        if (Time.time - lastSendTime > 0.05f)
        {
            // Gửi tọa độ hiện tại (transform.position luôn đúng vì rb.MovePosition cập nhật nó)
            var posData = new { x = transform.position.x, y = transform.position.y };
            string payload = JsonConvert.SerializeObject(posData);

            SocketClient.Instance.Send(new Packet
            {
                type = "MOVE",
                payload = payload
            });
            lastSendTime = Time.time;
        }
    }
}
[System.Serializable]
public struct PositionSnapshot
{
    public Vector3 position;
    public float timestamp;

    public PositionSnapshot(Vector3 pos, float time)
    {
        position = pos;
        timestamp = time;
    }
}