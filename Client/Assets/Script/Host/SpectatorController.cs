using UnityEngine;

public class SpectatorController : MonoBehaviour
{
    public float panSpeed = 20f;   // Movement speed
    public float zoomSpeed = 5f;   // Zoom speed
    public float minZoom = 5f;     // Min zoom
    public float maxZoom = 20f;    // Max zoom

    [Header("Map Limits (Relative to Spawn)")]
    public bool useMapLimit = true;
    [Tooltip("Left/Bottom limit relative to start (e.g. -50, -50)")]
    public Vector2 limitOffsetMin = new Vector2(-50, -50);
    [Tooltip("Right/Top limit relative to start (e.g. 50, 50)")]
    public Vector2 limitOffsetMax = new Vector2(50, 50);

    // Private vars to store actual bounds after adding spawn pos
    private Vector2 _actualMinBounds;
    private Vector2 _actualMaxBounds;

    private Camera myCam;

    void Start()
    {
        myCam = GetComponentInChildren<Camera>(); // Fixed: removed local declaration shadowing field
        if (myCam == null)
        {
            Debug.LogError("ERROR: This script must be attached to a GameObject with a Camera!");
        }

        // --- FIX AUDIO LISTENER CONFLICT ---
        // If this camera has a listener, disable others to prevent "2 Audio Listeners" warning
        AudioListener myListener = GetComponentInChildren<AudioListener>();
        if (myListener != null)
        {
            var allListeners = FindObjectsOfType<AudioListener>();
            foreach (var l in allListeners)
            {
                if (l != myListener && l.enabled)
                {
                    l.enabled = false;
                    Debug.Log($"🔊 SpectatorController: Disabled AudioListener on {l.gameObject.name} to avoid conflict.");
                }
            }
        }

        // Safety check: if spawned outside valid bounds, EXPAND bounds to include us
        // instead of resetting position (which causes the bug).
        if (useMapLimit)
        {
            // [NEW LOGIC] Calculate bounds relative to Spawn Point
            Vector2 spawnPos = transform.position;

            _actualMinBounds = spawnPos + limitOffsetMin;
            _actualMaxBounds = spawnPos + limitOffsetMax;

            Debug.Log($"📷 SpectatorController: Init Pos: {spawnPos}. Calculated Absolute Bounds: {_actualMinBounds} to {_actualMaxBounds}");
        }
        else
        {
            Debug.Log("📷 SpectatorController: Map Limit is OFF.");
        }
    }

    void Update()
    {
        // 1. Get keyboard input (WASD and Arrows)
        float h = Input.GetAxis("Horizontal"); // Left/Right
        float v = Input.GetAxis("Vertical");   // Up/Down

        // Debug input
        if (h != 0 || v != 0)
        {
            // Debug.Log($"Đang bấm di chuyển: {h}, {v}");
        }

        // 2. Calculate new position
        Vector3 pos = transform.position;
        pos.x += h * panSpeed * Time.deltaTime;
        pos.y += v * panSpeed * Time.deltaTime;

        if (useMapLimit)
        {
            pos.x = Mathf.Clamp(pos.x, _actualMinBounds.x, _actualMaxBounds.x);
            pos.y = Mathf.Clamp(pos.y, _actualMinBounds.y, _actualMaxBounds.y);
        }

        transform.position = pos;

        // 3. Handle Zoom (Scroll Wheel)
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