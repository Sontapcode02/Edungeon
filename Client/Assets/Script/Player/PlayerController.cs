using Cinemachine;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Singleton để truy cập nhanh từ các script khác (như MessageHandler)
    public static PlayerController LocalInstance;
    private HashSet<string> localFinishedMonsters = new HashSet<string>();
    [Header("Identity")]
    public string PlayerId;
    public bool IsLocal = false;

    [Header("Settings")]
    public float moveSpeed = 5f;
    public float networkSendInterval = 0.1f; // Tối ưu gửi 10 gói/giây

    [Header("State")]
    public string currentMonsterId; // Lưu ID/Tên con quái đang đụng độ
    private bool isProcessingCollision = false;

    [Header("Networking Smoothing")]
    private List<PositionSnapshot> serverSnapshots = new List<PositionSnapshot>();

    private Animator anim;
    private Rigidbody2D rb;
    private Vector3 lastPos;
    private float lastSendTime;
    private Vector2 currentInput;

    void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Khởi tạo nhân vật (Local hoặc Remote)
    /// </summary>
    public void Initialize(string id, bool local)
    {
        PlayerId = id;
        IsLocal = local;

        if (IsLocal)
        {
            LocalInstance = this;
            rb.bodyType = RigidbodyType2D.Dynamic; // Local dùng vật lý đầy đủ

            // Setup Camera
            var vcam = FindObjectOfType<CinemachineVirtualCamera>();
            if (vcam != null) vcam.Follow = transform;

            Debug.Log($"<color=green>[Local Player]</color> ID: {id} đã sẵn sàng.");
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic; // Remote chỉ nhận tọa độ
            serverSnapshots.Clear();
            serverSnapshots.Add(new PositionSnapshot(transform.position, Time.time));
        }

        lastPos = transform.position;
    }

    void Update()
    {
        if (IsLocal)
        {
            // 1. Đọc Input
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            currentInput = new Vector2(h, v).normalized;
        }
        else
        {
            // 2. Nội suy vị trí cho người chơi khác
            InterpolateMovement();
        }

        // 3. Xử lý Animation cho tất cả
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (IsLocal)
        {
            MoveLocalPlayer();
        }
    }

    // --- DI CHUYỂN BẢN THÂN ---
    void MoveLocalPlayer()
    {
        rb.MovePosition(rb.position + currentInput * moveSpeed * Time.fixedDeltaTime);

        // Gửi tọa độ lên Server theo chu kỳ
        if (Time.time - lastSendTime > networkSendInterval)
        {
            if (currentInput != Vector2.zero || Vector3.Distance(transform.position, lastPos) > 0.01f)
            {
                SendPosition();
                lastPos = transform.position;
            }
        }
    }

    void SendPosition()
    {
        var posData = new { x = transform.position.x, y = transform.position.y };
        SocketClient.Instance.Send(new Packet
        {
            type = "MOVE",
            payload = JsonConvert.SerializeObject(posData)
        });
        lastSendTime = Time.time;
    }

    // --- NỘI SUY NGƯỜI CHƠI KHÁC ---
    public void OnServerDataReceived(Vector3 newPos)
    {
        if (IsLocal) return;
        serverSnapshots.Add(new PositionSnapshot(newPos, Time.time));
        if (serverSnapshots.Count > 10) serverSnapshots.RemoveAt(0);
    }

    void InterpolateMovement()
    {
        float renderTime = Time.time - networkSendInterval;
        if (serverSnapshots.Count >= 2)
        {
            PositionSnapshot a = serverSnapshots[0];
            PositionSnapshot b = serverSnapshots[0];

            for (int i = serverSnapshots.Count - 1; i >= 1; i--)
            {
                if (serverSnapshots[i].timestamp <= renderTime)
                {
                    a = serverSnapshots[i];
                    b = (i + 1 < serverSnapshots.Count) ? serverSnapshots[i + 1] : serverSnapshots[i];
                    break;
                }
            }

            float t = (renderTime - a.timestamp) / (b.timestamp - a.timestamp);
            if (float.IsNaN(t) || float.IsInfinity(t)) t = 1;
            transform.position = Vector3.Lerp(a.position, b.position, t);
        }
    }

    // --- ANIMATION ---
    void UpdateAnimation()
    {
        if (anim == null) return;

        Vector2 moveDir = Vector2.zero;
        if (IsLocal) moveDir = currentInput;
        else
        {
            moveDir = (transform.position - lastPos);
            lastPos = transform.position;
        }

        if (moveDir.sqrMagnitude > 0.001f)
        {
            anim.SetFloat("InputX", moveDir.x);
            anim.SetFloat("InputY", moveDir.y);
            anim.speed = 1f;
        }
        else
        {
            anim.speed = 0f; // Dừng animation khi đứng im
        }
    }

    // --- VA CHẠM QUÁI ---
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsLocal || isProcessingCollision) return;

        if (collision.CompareTag("Enemy"))
        {
            string mId = collision.gameObject.name;

            // KIỂM TRA: Nếu con quái này mình làm xong rồi thì thôi, không xin câu hỏi nữa
            if (localFinishedMonsters.Contains(mId))
            {
                Debug.Log($"<color=cyan>Đại ca ơi, con {mId} này mình làm rồi, đi tiếp thôi!</color>");
                return;
            }

            isProcessingCollision = true;
            currentMonsterId = mId;

            SocketClient.Instance.Send(new Packet
            {
                type = "REQUEST_QUESTION",
                payload = currentMonsterId
            });

            StartCoroutine(ResetCollisionFlag(1.5f));
        }
    }
    public void MarkMonsterAsFinished(string monsterName)
    {
        if (!localFinishedMonsters.Contains(monsterName))
        {
            localFinishedMonsters.Add(monsterName);

            // HIỆU ỨNG: Làm mờ con quái đó đi (Chỉ máy đại ca thấy mờ)
            GameObject monster = GameObject.Find(monsterName);
            if (monster != null)
            {
                var renderer = monster.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Biến thành bóng ma
                }
            }
        }
    }

    IEnumerator ResetCollisionFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        isProcessingCollision = false;
    }
}

[System.Serializable]
public struct PositionSnapshot
{
    public Vector3 position;
    public float timestamp;
    public PositionSnapshot(Vector3 pos, float time) { position = pos; timestamp = time; }
}