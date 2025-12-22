using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class MessageHandler : MonoBehaviour
{
    [Header("Host Settings")]
    public GameObject spectatorCameraPrefab;
    public GameObject hostControlUI;
    public Transform hostSpawnPoint;

    [Header("Player Settings")]
    // Lưu ý: Biến này kiểu PlayerController thì Instantiate sẽ ra PlayerController
    public PlayerController playerPrefab;
    public Transform playersContainer;
    public Transform spawnPoint;

    [Header("UI")]
    public QuizUI quizUI;
    public LeaderboardUI leaderboardUI;

    [Header("Host UI")]
    public TextMeshProUGUI idRoom;

    private bool isAmHost = false;

    // Dictionary quản lý nhân vật (Key: ID, Value: Script điều khiển)
    private Dictionary<string, PlayerController> otherPlayers = new Dictionary<string, PlayerController>();

    void Start()
    {
        // 1. Lấy dữ liệu từ Home gửi sang
        string myName = PlayerPrefs.GetString("PLAYER_NAME", "Unknown");
        string myRoom = PlayerPrefs.GetString("ROOM_ID", "Default");
        isAmHost = PlayerPrefs.GetInt("IS_HOST", 0) == 1;

        Debug.Log($"Game Init: {myName} | Room: {myRoom} | IsHost: {isAmHost}");

        // 2. Setup giao diện ban đầu
        SetupRoleUI();
        if (idRoom) idRoom.text = "ID: " + myRoom;

        // 3. Kích hoạt kết nối mạng
        SocketClient.Instance.OnPacketReceived += HandlePacket;
        // Gửi lệnh Join hoặc Create tùy vai trò (đã xử lý bên ConnectAndJoin)
        SocketClient.Instance.ConnectAndJoin(myName, myRoom, isAmHost);
    }

    void OnDestroy()
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.OnPacketReceived -= HandlePacket;
        }
    }

    void SetupRoleUI()
    {
        if (isAmHost)
        {
            Debug.Log("Host mode: Bật camera quan sát.");
            if (spectatorCameraPrefab)
            {
                Vector3 spawnPos = (hostSpawnPoint != null) ? hostSpawnPoint.position : new Vector3(0, 0, -10);
                Instantiate(spectatorCameraPrefab, spawnPos, Quaternion.identity);
            }
            if (hostControlUI) hostControlUI.SetActive(true);
        }
        else
        {
            if (hostControlUI) hostControlUI.SetActive(false);
        }
    }

    void HandlePacket(Packet packet)
    {
        switch (packet.type)
        {
            case "GAME_STARTED": // Sửa cho khớp với Server (GAME_STARTED)
                Debug.Log("Game Started!");
                // Ẩn UI chờ, hiện UI game (nếu cần)
                break;

            case "ROOM_DESTROYED":
                Debug.LogWarning("Phòng đã bị hủy: " + packet.payload);
                BackToHome();
                break;

            case "ERROR":
                Debug.LogError("Lỗi từ Server: " + packet.payload);
                BackToHome();
                break;

            case "MOVE":
                UpdatePlayerPosition(packet);
                break;

            case "ANSWER_RESULT":
                if (quizUI) quizUI.ShowResult(packet.payload);
                break;

            case "LEADERBOARD_UPDATE":
                if (leaderboardUI) leaderboardUI.UpdateList(packet.payload);
                break;

            case "SYNC_PLAYERS":
                // Nhận danh sách người cũ khi mới vào
                var list = JsonConvert.DeserializeObject<List<PlayerState>>(packet.payload);
                foreach (var state in list)
                {
                    SpawnPlayer(state.playerId, state.name, new Vector2(state.x, state.y));
                }
                break;

            case "PLAYER_JOINED":
                // Có người mới vào sau mình
                var newState = JsonConvert.DeserializeObject<PlayerState>(packet.payload);
                Vector2 spawnPos = Vector2.zero;

                if (spawnPoint != null)
                {
                    spawnPos = spawnPoint.position;
                }
                else
                {
                    Debug.LogWarning("Chưa kéo SpawnPoint vào MessageHandler kìa đại ca!");
                }

                // Gọi hàm spawn
                SpawnPlayer(newState.playerId, newState.name, spawnPos);
                break;
            case "JOIN_SUCCESS":
                {
                    string myId = packet.playerId;
                    Debug.Log($">> [Client] Joined success! MyID is: {myId}");
                    SocketClient.Instance.MyPlayerId = myId;
                    if (otherPlayers.ContainsKey(myId))
                    {
                        Debug.Log(">> [FIX] Phát hiện nhân vật của mình đã spawn trước -> Kích hoạt quyền điều khiển!");

                        PlayerController myPlayerScript = otherPlayers[myId];

                        // Bật lại chế độ Local (Gắn camera, bật điều khiển)
                        myPlayerScript.Initialize(myId, true);

                        // Xóa khỏi danh sách "người khác" (vì đây là mình mà)
                        otherPlayers.Remove(myId);
                    }
                    else
                    {
                        // Trường hợp hiếm: Nếu JOIN_SUCCESS đến trước PLAYER_JOINED thì spawn luôn
                        SpawnPlayer(myId, "Me", Vector2.zero);
                    }
                    break;
                   
                }
            case "ROOM_CREATED": // <-- Thêm cái này cho Host
                string hostId = packet.playerId;
                Debug.Log($">> [Host] Room created! Server gave ID: {hostId}");
                SocketClient.Instance.MyPlayerId = hostId;

                break;
            case "PLAYER_LEFT":
                string leftId = packet.payload;
                if (otherPlayers.ContainsKey(leftId))
                {
                    Destroy(otherPlayers[leftId].gameObject); // Xóa nhân vật
                    otherPlayers.Remove(leftId); // Xóa khỏi list
                }
                break;
        }
    }

    // --- HÀM SPAWN ĐÃ ĐƯỢC SỬA LỖI ---
    void SpawnPlayer(string id, string playerName, Vector2 initialPos)
    {
        if (id.StartsWith("Host_")) return;
        if (id == SocketClient.Instance.MyPlayerId && isAmHost) return;
        if (id == SocketClient.Instance.MyPlayerId && !isAmHost) return;
        if (otherPlayers.ContainsKey(id)) return;

        Vector3 finalPos = (initialPos == Vector2.zero && spawnPoint != null)
                           ? spawnPoint.position
                           : (Vector3)initialPos;
        PlayerController pCtrl = Instantiate(playerPrefab, finalPos, Quaternion.identity, playersContainer);

        // Khởi tạo (Lúc này hàm Initialize bên PlayerController sẽ tự gọi Cinemachine)
        bool isMe = (id == SocketClient.Instance.MyPlayerId);
        pCtrl.Initialize(id, isMe);

        otherPlayers.Add(id, pCtrl);
    }

    // --- HÀM UPDATE VỊ TRÍ (ĐÃ HOÀN THIỆN) ---
    void UpdatePlayerPosition(Packet packet)
    {
        var state = JsonConvert.DeserializeObject<PlayerState>(packet.payload);

        if (otherPlayers.TryGetValue(packet.playerId, out PlayerController playerScript))
        {
            if (playerScript.IsLocal) return;

            // GỌI HÀM MỚI
            playerScript.OnServerDataReceived(new Vector3(state.x, state.y, 0));
        }
    }

    void BackToHome()
    {
        if (SocketClient.Instance) SocketClient.Instance.OnPacketReceived -= HandlePacket;
        SceneManager.LoadScene("Home");
    }
}


