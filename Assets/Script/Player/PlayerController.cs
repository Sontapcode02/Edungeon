using UnityEngine;
using Newtonsoft.Json;
using Cinemachine;

public class PlayerController : MonoBehaviour
{
    public string PlayerId;
    public bool IsLocal;
    public float speed = 5f;

    void Update()
    {
        if (!IsLocal) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (h != 0 || v != 0)
        {
            Debug.Log($"Đang bấm phím! H: {h}, V: {v}");
            Vector3 movement = new Vector3(h, v, 0) * speed * Time.deltaTime;
            transform.position += movement;

            // Send Network Packet
            SendPosition();
        }
    }

    public void Initialize(string id, bool local)
    {
        PlayerId = id;
        IsLocal = local;
        CinemachineVirtualCamera vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = transform; 
        }
        GetComponent<PlayerMovement>().enabled = true;
    }

    // Rate limit sending (e.g., every 0.1s) to avoid flooding
    float lastSendTime;
    void SendPosition()
    {
        if (Time.time - lastSendTime > 0.05f)
        {
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

    public void ApplySpeedBoost()
    {
        speed *= 2;
        Invoke("ResetSpeed", 5f); // 5 seconds duration
    }

    void ResetSpeed() => speed /= 2;
}