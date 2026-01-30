using Cinemachine;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Singleton for quick access from other scripts (like MessageHandler)
    public static PlayerController LocalInstance;
    public bool isPaused = false;
    private HashSet<string> localFinishedMonsters = new HashSet<string>();
    [Header("Identity")]
    public string PlayerId;
    public string PlayerName;
    public bool IsLocal = false;
    public TMPro.TextMeshPro playerNameText; // Reference to TextMeshPro component

    [Header("Settings")]
    public float moveSpeed = 5f;
    public float networkSendInterval = 0.1f; // Optimized to send 10 packets/second

    [Header("State")]
    public string currentMonsterId; // Store ID/Name of current monster encounter
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

        // [COLLISION SETUP]
        // Set Player to Layer 3 (Unnamed User Layer)
        int playerLayer = 3;
        gameObject.layer = playerLayer;
        // Ignore collision between Player Layer vs Player Layer
        Physics2D.IgnoreLayerCollision(playerLayer, playerLayer, true);

        // Auto-find TextMeshPro if not assigned
        if (playerNameText == null)
        {
            playerNameText = GetComponentInChildren<TMPro.TextMeshPro>();
            if (playerNameText == null) Debug.LogWarning($"⚠️ PlayerController: No TextMeshPro found on {gameObject.name}");
            else Debug.Log($"✅ PlayerController: Found TextMeshPro on {gameObject.name}");
        }

        // Ensure Text is visible above sprites
        if (playerNameText != null)
        {
            var meshRenderer = playerNameText.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                // Force VERY high sorting order to cover everything
                meshRenderer.sortingOrder = 5000;
                meshRenderer.sortingLayerName = "Default";

                // Also check if text is on the "UI" layer instead of "Default"
                // If your Camera is culling the UI layer, it won't show.
                // Let's force it to "Default" layer (Layer 0)
                playerNameText.gameObject.layer = 0;
            }
        }
    }

    /// <summary>
    /// Initialize Player (Local or Remote)
    /// </summary>
    public void Initialize(string id, string name, bool local)
    {
        PlayerId = id;
        PlayerName = name;
        IsLocal = local;

        // --- NAME TAG SETUP ---
        if (playerNameText != null)
        {
            Debug.Log($"🏷️ Setting NameTag for {id}: {name}");
            playerNameText.text = name;
            // playerNameText.fontSize = 5; // [REMOVED] Used Prefab settings instead

            // Text alignment
            playerNameText.alignment = TMPro.TextAlignmentOptions.Center;

            // Optionally: Set color for local player vs others
            if (IsLocal) playerNameText.color = Color.green;
            else playerNameText.color = Color.white;
        }
        else
        {
            Debug.LogError($"❌ Cannot set name '{name}' because playerNameText is NULL on {gameObject.name}");
        }

        if (IsLocal)
        {
            LocalInstance = this;
            rb.bodyType = RigidbodyType2D.Dynamic; // Local uses full physics

            // Setup Camera
            var vcam = FindObjectOfType<CinemachineVirtualCamera>();
            if (vcam != null) vcam.Follow = transform;

            Debug.Log($"<color=green>[Local Player]</color> ID: {id}, Name: {name} is ready.");
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic; // Remote only receives coordinates
            serverSnapshots.Clear();
            serverSnapshots.Add(new PositionSnapshot(transform.position, Time.time));
        }

        lastPos = transform.position;
    }

    void Update()
    {
        if (IsLocal)
        {
            // 1. Read Input
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            currentInput = new Vector2(h, v).normalized;
        }
        else
        {
            // 2. Interpolate position for other players
            InterpolateMovement();
        }
        if (isPaused)
        {
            // Reset velocity to 0 to prevent drifting
            rb.velocity = Vector2.zero;
            return;
        }

        // 3. Handle Animation for all
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        // [FIX] Stop Physics calculation when Paused
        if (isPaused) return;

        if (IsLocal)
        {
            MoveLocalPlayer();
        }
    }

    // --- MOVE SELF ---
    void MoveLocalPlayer()
    {
        rb.MovePosition(rb.position + currentInput * moveSpeed * Time.fixedDeltaTime);

        // Send position to Server periodically
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

    // --- INTERPOLATE OTHER PLAYERS ---
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
            anim.speed = 0f; // Stop animation when standing still
        }
    }

    // --- MONSTER COLLISION ---
    [Header("Audio")]
    public AudioClip enemyCollisionSFX;
    public AudioClip gameFinishSFX;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsLocal || isProcessingCollision) return;

        if (collision.CompareTag("Enemy"))
        {
            string mId = collision.gameObject.name;

            // CHECK: If this monster is already finished, don't ask for question again
            if (localFinishedMonsters.Contains(mId))
            {
                Debug.Log($"<color=cyan>Hey boss, we finished {mId} already, let's keep moving!</color>");
                return;
            }

            // --- AUDIO ---
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(enemyCollisionSFX);

            isProcessingCollision = true;
            currentMonsterId = mId;

            SocketClient.Instance.Send(new Packet
            {
                type = "REQUEST_QUESTION",
                payload = currentMonsterId
            });

            StartCoroutine(ResetCollisionFlag(1.5f));
        }


        if (collision.CompareTag("Finish"))
        {
            Debug.Log("<color=green>🏁 CONGRATULATIONS! YOU REACHED THE FINISH LINE!</color>");

            // --- AUDIO ---
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(gameFinishSFX);

            // Send command to Server to calculate time and ranking
            SocketClient.Instance.Send(new Packet
            {
                type = "REACHED_FINISH",
                payload = ""
            });
        }
    }
    public void MarkMonsterAsFinished(string monsterName)
    {
        if (!localFinishedMonsters.Contains(monsterName))
        {
            localFinishedMonsters.Add(monsterName);

            // EFFECT: Fade out the monster (Local view only)
            GameObject monster = GameObject.Find(monsterName);
            if (monster != null)
            {
                var renderer = monster.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Turn into ghost
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