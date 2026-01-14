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

    [Header("Network Smoothing")]
    private List<PositionSnapshot> serverSnapshots = new List<PositionSnapshot>();

    private Animator anim;
    private Rigidbody2D rb;
    private Vector3 lastPos;
    private float lastSendTime;

    // Biến lưu input để dùng giữa Update và FixedUpdate
    private Vector2 currentInput;

    void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError($"[BẮT QUẢ TANG] Thằng '{gameObject.name}' (Parent: {transform.parent?.name}) đang kêu gào vì thiếu Rigidbody2D!");
            this.enabled = false;
        }
    }

    public void Initialize(string id, bool local)
    {
        PlayerId = id;
        IsLocal = local;
        Debug.Log($"[PLAYER] Init ID: {id} | IsLocal: {IsLocal}");
        serverSnapshots.Clear();
        serverSnapshots.Add(new PositionSnapshot(transform.position, Time.time));

        lastPos = transform.position;

        if (IsLocal)
        {
            // --- SETUP CHO LOCAL PLAYER ---
            rb.bodyType = RigidbodyType2D.Dynamic; // Để va chạm vật lý

            var vcam = FindObjectOfType<CinemachineVirtualCamera>();
            if (vcam != null)
            {
                vcam.Follow = transform;
                Debug.Log("Cinemachine đã nhận mục tiêu: " + id);
            }
        }
        else
        {
            // --- SETUP CHO REMOTE PLAYER ---
            // Quan trọng: Biến thành Kinematic để không bị vật lý đẩy lung tung
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
        }
    }

    public void OnServerDataReceived(Vector3 newPos)
    {
        if (IsLocal) return; // Local thì không nghe Server chỉ đạo vị trí (tránh giật)
        Debug.Log($"[PLAYER] {PlayerId} nhận tọa độ mới: {newPos}");
        serverSnapshots.Add(new PositionSnapshot(newPos, Time.time));

        // Dọn dẹp snapshot cũ
        if (serverSnapshots.Count > 20)
        {
            serverSnapshots.RemoveAt(0);
        }
    }

    void Update()
    {
        // 1. Xử lý Logic từng frame
        if (IsLocal)
        {
            // Nếu là mình: Chỉ đọc Input (để dành cho FixedUpdate xử lý vật lý)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            currentInput = new Vector2(h, v).normalized;
        }
        else
        {
            // Nếu là người khác: Tính toán vị trí mượt (Interpolation)
            InterpolateMovement();
        }

        // 2. Xử lý Animation (chung cho cả 2)
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        // Logic vật lý chỉ chạy cho Local Player
        if (IsLocal)
        {
            MoveLocalPlayer();
        }
    }

    // --- LOGIC DI CHUYỂN LOCAL ---
    void MoveLocalPlayer()
    {
        // Di chuyển bằng Rigidbody
        rb.MovePosition(rb.position + currentInput * moveSpeed * Time.fixedDeltaTime);

        // Gửi vị trí lên Server (Chỉ gửi khi có di chuyển hoặc vừa dừng lại)
        // Thêm điều kiện: Nếu input khác 0 hoặc (input = 0 nhưng frame trước vừa di chuyển)
        if (currentInput != Vector2.zero || (Time.time - lastSendTime > 0.1f))
        {
            SendPosition();
        }
    }

    // --- LOGIC DI CHUYỂN REMOTE (MƯỢT) ---
    void InterpolateMovement()
    {
        float renderTime = Time.time - 0.1f; // Độ trễ giả lập 100ms để mượt

        if (serverSnapshots.Count >= 2)
        {
            PositionSnapshot snapshotA = serverSnapshots[0];
            PositionSnapshot snapshotB = serverSnapshots[0];

            // Tìm 2 điểm bao quanh thời gian renderTime
            for (int i = serverSnapshots.Count - 1; i >= 1; i--)
            {
                if (serverSnapshots[i].timestamp <= renderTime)
                {
                    snapshotA = serverSnapshots[i];
                    if (i + 1 < serverSnapshots.Count)
                        snapshotB = serverSnapshots[i + 1];
                    else
                        snapshotB = serverSnapshots[i];
                    break;
                }
            }

            float timeInterval = snapshotB.timestamp - snapshotA.timestamp;
            if (timeInterval > 0.0001f)
            {
                float t = (renderTime - snapshotA.timestamp) / timeInterval;
                transform.position = Vector3.Lerp(snapshotA.position, snapshotB.position, t);
            }
            else
            {
                transform.position = snapshotB.position;
            }
        }
        else if (serverSnapshots.Count == 1)
        {
            transform.position = Vector3.Lerp(transform.position, serverSnapshots[0].position, Time.deltaTime * 10f);
        }
    }

    void UpdateAnimation()
    {
        if (anim == null) return;

        float moveAmount = 0;
        Vector2 dir = Vector2.zero;

        if (IsLocal)
        {
            if (currentInput.sqrMagnitude > 0.01f)
            {
                moveAmount = 1f;
                dir = currentInput;
            }
        }
        else
        {
            // Tính toán dựa trên khoảng cách thực tế di chuyển được
            float dist = Vector3.Distance(transform.position, lastPos);
            if (dist > 0.001f) // Giảm ngưỡng xuống tí cho nhạy
            {
                moveAmount = 1f; // Chỉ cần có di chuyển là chạy
                dir = (transform.position - lastPos).normalized;
            }
            lastPos = transform.position;
        }

        if (moveAmount > 0.01f)
        {
            anim.SetFloat("InputX", dir.x);
            anim.SetFloat("InputY", dir.y);
            anim.speed = 1f; // Chạy animation
        }
        else
        {
            anim.speed = 0f; // Dừng animation (đứng yên frame cuối)
        }
    }

    void SendPosition()
    {
        // [QUAN TRỌNG] Thêm dòng này để chặn gửi nếu đứng im (tránh spam log do rơi tự do)
        // Biến currentInput lấy từ hàm Update()
        if (currentInput == Vector2.zero) return;

        // Giới hạn tốc độ gửi (0.05s)
        if (Time.time - lastSendTime > 0.05f)
        {
            var posData = new { x = transform.position.x, y = transform.position.y };
            string payload = JsonConvert.SerializeObject(posData);

            // --- THÊM LOG NÀY ĐỂ KIỂM TRA ---
            Debug.Log($"[GUEST] 📤 Đang gửi MOVE lên Server! Payload: {payload}");

            SocketClient.Instance.Send(new Packet
            {
                type = "MOVE",
                payload = payload
            });
            lastSendTime = Time.time;
        }
    }

    // --- XỬ LÝ VA CHẠM ---
    void OnTriggerEnter2D(Collider2D collision)
    {
        // Chỉ Local Player mới được quyền báo cáo va chạm lên Server
        if (!IsLocal) return;

        if (collision.CompareTag("Enemy"))
        {
            Debug.Log("Đụng trúng địch!");
            SocketClient.Instance.Send(new Packet
            {
                type = "ENEMY_ENCOUNTER",
                payload = ""
            });
        }

        if (collision.CompareTag("Finish"))
        {
            Debug.Log("Về đích!");
            SocketClient.Instance.Send(new Packet
            {
                type = "REACH_FINISH",
                payload = ""
            });
        }
    }
}

// Struct dữ liệu snapshot
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