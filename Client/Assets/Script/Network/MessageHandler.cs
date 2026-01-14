using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class MessageHandler : MonoBehaviour
{
    [Header("Game fllow object")]
    public GameObject lobbyGate;  
    public GameObject enemyContainer;

    [Header("Host Settings")]
    public GameObject spectatorCameraPrefab;
    public GameObject hostControlUI;
    public Transform hostSpawnPoint;

    [Header("Player Settings")]
    // Lưu ý: Biến này kiểu PlayerController thì Instantiate sẽ ra PlayerController
    public GameObject playerPrefab;  // Nếu prefab là GameObject
    public Transform playersContainer;
    public Transform spawnPoint;

    [Header("UI")]
    public QuizUI quizUI;
    public LeaderboardUI leaderboardUI;
    public UnityEngine.UI.Button leaveRoomButton;
    [Header("Host UI")]
    public TextMeshProUGUI idRoom;

    private bool isAmHost = false;

    private Dictionary<string, PlayerController> otherPlayers = new Dictionary<string, PlayerController>();

    void Start()
    {
        enemyContainer.SetActive(false);
        
        // 1. Lấy dữ liệu từ Home gửi sang
        string myName = PlayerPrefs.GetString("PLAYER_NAME", "Unknown");
        string myRoom = PlayerPrefs.GetString("ROOM_ID", "Default");
        isAmHost = PlayerPrefs.GetInt("IS_HOST", 0) == 1;

        Debug.Log($"Game Init: {myName} | Room: {myRoom} | IsHost: {isAmHost}");

        // 2. Setup giao diện ban đầu
        SetupRoleUI();
        if (idRoom) idRoom.text = myRoom;
        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.AddListener(OnLeaveButtonClicked);
        }
        else
        {
            Debug.LogWarning("Chưa gắn nút LeaveRoomButton vào MessageHandler kìa đại ca!");
        }

        // 3. Kích hoạt kết nối mạng
        SocketClient.Instance.OnPacketReceived += HandlePacket;

        // --- FIX: Gửi lệnh JOIN_ROOM cho Guest ---
        if (!isAmHost)
        {
            // Nếu là Khách -> Gửi lệnh Join để Server thực sự add vào phòng
            Debug.Log(">>> Tôi là Khách, đang gửi lệnh xin vào phòng...");
            SocketClient.Instance.SendJoinRoom(myName, myRoom);
        }
        else
        {

            Debug.Log(">>> Tôi là Host, đang chờ nhận dữ liệu từ Server...");
        }
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
            case "START_GAME":
                Debug.Log(">>> Nhận lệnh START_GAME từ Server! Chuyển cảnh ngay!");
                break;
            case "OPEN_GATE":
                if (lobbyGate != null)
                {
                    Debug.Log("Server lệnh mở cổng!");
                    lobbyGate.SetActive(false);
                    enemyContainer.SetActive(true);
                }
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
                if (packet.playerId == SocketClient.Instance.MyPlayerId)
                {
                    return;
                }
                UpdatePlayerPosition(packet);
                break;

            case "CHAT_RECEIVE":
                ChatManager.Instance.OnMessageReceived(packet.payload);
                break;

            case "CHAT_STATUS":
                bool isMuted = (packet.payload == "MUTED");
                ChatManager.Instance.UpdateChatStatus(isMuted);
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
                    SpawnPlayer(state.playerId, state.playerName, new Vector2(state.x, state.y));
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
                SpawnPlayer(newState.playerId, newState.playerName, spawnPos);
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

            // Thêm case mới trong HandlePacket():
            case "LOAD_QUIZ":
                // Server gửi dữ liệu quiz
                QuizManager.Instance.LoadQuizzesFromCSV(packet.payload);
                Debug.Log(">>> Đã tải bộ câu hỏi từ Server!");
                break;

            case "PROGRESS_UPDATE":
                if (leaderboardUI)
                {
                    leaderboardUI.UpdateList(packet.payload);
                }
                break;

            case "PLAYER_DIED":
                Debug.Log($"Player {packet.payload} đã bị loại!");
                if (otherPlayers.ContainsKey(packet.payload))
                {
                    Destroy(otherPlayers[packet.payload].gameObject);
                    otherPlayers.Remove(packet.payload);
                }
                break;

            case "GAME_COMPLETED":
                Debug.Log($">>> {packet.payload} đã hoàn thành game!");
                SceneManager.LoadScene("GameEnd");
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
        GameObject playerObj = Instantiate(playerPrefab, finalPos, Quaternion.identity, playersContainer);
        PlayerController pCtrl = playerObj.GetComponent<PlayerController>();

        // Khởi tạo (Lúc này hàm Initialize bên PlayerController sẽ tự gọi Cinemachine)
        bool isMe = (id == SocketClient.Instance.MyPlayerId);
        pCtrl.Initialize(id, isMe);

        otherPlayers.Add(id, pCtrl);
    }


    void UpdatePlayerPosition(Packet packet)
    {
        Debug.Log($"[DEBUG] Nhận gói MOVE từ ID: {packet.playerId}");
        var state = JsonConvert.DeserializeObject<PlayerState>(packet.payload);

        // Trường hợp 1: Đã có nhân vật này trong danh sách -> Cập nhật
        if (otherPlayers.TryGetValue(packet.playerId, out PlayerController playerScript))
        {
            if (playerScript.IsLocal) return; // Nếu là mình thì bỏ qua

            // Gọi hàm cập nhật vị trí (nhớ là bên PlayerController phải dùng Lerp cho mượt nhé)
            playerScript.OnServerDataReceived(new Vector3(state.x, state.y, 0));
        }
        // Trường hợp 2: Chưa có nhân vật này (do gói MOVE đến trước gói JOIN) -> Spawn luôn!
        else
        {
            // Chỉ Spawn nếu đó KHÔNG phải là mình
            if (packet.playerId != SocketClient.Instance.MyPlayerId)
            {
                Debug.Log($"[Fix] Nhận MOVE từ {packet.playerId} nhưng chưa thấy người. Đang Spawn gấp...");

                // Spawn ngay tại vị trí mới nhận được
                SpawnPlayer(packet.playerId, "Player", new Vector2(state.x, state.y));

                // Sau khi spawn xong thì cập nhật vị trí lần nữa cho chắc
                if (otherPlayers.TryGetValue(packet.playerId, out PlayerController newScript))
                {
                    newScript.OnServerDataReceived(new Vector3(state.x, state.y, 0));
                }
            }
        }
    }

    public void OnLeaveButtonClicked()
    {
        Debug.Log(">>> Bấm nút rời phòng!");
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.Disconnect();
        }

        // 2. Quay về Home (Dùng lại hàm BackToHome có sẵn)
        BackToHome();
    }
    void BackToHome()
    {
        if (SocketClient.Instance) SocketClient.Instance.OnPacketReceived -= HandlePacket;
        SceneManager.LoadScene("Home");
    }
}


