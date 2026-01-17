using Newtonsoft.Json;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class MessageHandler : MonoBehaviour
{
    [Header("Game flow object")]
    public GameObject lobbyGate;
    public GameObject enemyContainer;

    [Header("Host Settings")]
    public GameObject spectatorCameraPrefab;
    public GameObject hostControlUI;
    public Transform hostSpawnPoint;

    [Header("Player Settings")]
    public GameObject playerPrefab;
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
        if (enemyContainer) enemyContainer.SetActive(false);

        string myName = PlayerPrefs.GetString("PLAYER_NAME", "Unknown");
        string myRoom = PlayerPrefs.GetString("ROOM_ID", "Default");
        isAmHost = PlayerPrefs.GetInt("IS_HOST", 0) == 1;

        SetupRoleUI();
        if (idRoom) idRoom.text = myRoom;

        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(OnLeaveButtonClicked);

        SocketClient.Instance.OnPacketReceived += HandlePacket;

        if (!isAmHost)
        {
            SocketClient.Instance.SendJoinRoom(myName, myRoom);
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

    // --- HÀM XỬ LÝ GÓI TIN (ĐÃ FIX LỖI JSON) ---
    void HandlePacket(Packet packet)
    {
        switch (packet.type)
        {
            // [QUAN TRỌNG] Các case này trả về string đơn giản (Success/FOUND)
            // TUYỆT ĐỐI KHÔNG dùng JsonConvert.DeserializeObject ở đây!

            case "CHECK_ROOM_RESPONSE":
                // Payload là "FOUND" hoặc "NOT_FOUND" -> Dùng luôn
                SocketClient.Instance.OnCheckRoomResult?.Invoke(packet.payload);
                break;

            case "ROOM_CREATED":
                // Payload là "Success" -> Dùng luôn, đừng Deserialize!
                string hostId = packet.playerId;
                SocketClient.Instance.MyPlayerId = hostId;

                // Báo cho HomeUIManager biết là thành công
                SocketClient.Instance.OnCreateRoomResult?.Invoke("SUCCESS");
                break;

            case "JOIN_SUCCESS":
                // Payload là "Success"
                string myId = packet.playerId;
                SocketClient.Instance.MyPlayerId = myId;

                // Xử lý nhân vật
                if (otherPlayers.ContainsKey(myId))
                {
                    PlayerController myPlayerScript = otherPlayers[myId];
                    myPlayerScript.Initialize(myId, true);
                    otherPlayers.Remove(myId);
                }
                else
                {
                    SpawnPlayer(myId, "Me", Vector2.zero);
                }
                break;
            // -----------------------------------------------------------

            case "ERROR":
                string errorMsg = packet.payload;
                if (errorMsg.Contains("đã đánh bại"))
                {
                    Debug.Log($"<color=cyan>[Hệ thống]</color>: {errorMsg}");
                    // Nếu đại ca có script Toast hoặc Popup thông báo, hãy gọi ở đây
                    // UIHint.Show(errorMsg); 

                    // TIÊU DIỆT con quái "ma" này trên màn hình đại ca luôn
                    GameObject monster = GameObject.Find(PlayerController.LocalInstance.currentMonsterId);
                    if (monster != null) monster.SetActive(false);
                }
                else
                {
                    Debug.LogError("Server báo lỗi: " + errorMsg);
                }
                break;

            case "START_GAME":
                Debug.Log("Game Start!");
                break;
            case "GAME_PAUSED":
                Debug.Log("<color=yellow>⚠️ TRẬN ĐẤU TẠM DỪNG!</color>");

                // 1. Khóa di chuyển của đại ca
                if (PlayerController.LocalInstance != null)
                    PlayerController.LocalInstance.isPaused = true;

                // 2. Hiện thông báo UI (Nếu đại ca có bảng Popup)
                string pauseMsg = packet.payload; // "Trận đấu tạm dừng!"
                if (NotificationUI.Instance != null)
                    NotificationUI.Instance.ShowMessage(pauseMsg, false); // false = không tự tắt
                break;

            case "GAME_RESUMED":
                Debug.Log("<color=green>▶️ TIẾP TỤC ĐUA!</color>");

                // 1. Mở khóa di chuyển
                if (PlayerController.LocalInstance != null)
                    PlayerController.LocalInstance.isPaused = false;

                // 2. Tắt thông báo UI
                if (NotificationUI.Instance != null)
                    NotificationUI.Instance.HideMessage();
                break;

            case "OPEN_GATE":
                Debug.Log("<color=cyan>🔔 Game sắp bắt đầu! Chuẩn bị đếm ngược...</color>");
                if (enemyContainer != null)
                {
                    enemyContainer.SetActive(true);
                }
                // 1. Đảm bảo nhân vật đang bị khóa (isPaused = true)
                if (PlayerController.LocalInstance != null)
                    PlayerController.LocalInstance.isPaused = true;

                // 2. Bắt đầu đếm ngược 3 giây
                if (NotificationUI.Instance != null)
                {
                    NotificationUI.Instance.StartCountdown(3, () => {
                        if (lobbyGate != null)
                        {
                            lobbyGate.SetActive(false); // Làm cái cổng biến mất
                                                         // Hoặc nếu muốn chuyên nghiệp hơn, đại ca dùng:
                                                         // gateObject.GetComponent<Collider2D>().enabled = false; 
                            Debug.Log("<color=yellow>🚪 CỔNG ĐÃ MỞ!</color>");
                        }
                        // 3. Khi đếm xong (onFinished), mới cho phép di chuyển
                        if (PlayerController.LocalInstance != null)
                            PlayerController.LocalInstance.isPaused = false;

                        Debug.Log("<color=green>🔥 XUẤT PHÁT!</color>");
                    });
                }
                break;

            case "ROOM_DESTROYED":
                BackToHome();
                break;

            case "MOVE":
                if (packet.playerId == SocketClient.Instance.MyPlayerId) return;
                UpdatePlayerPosition(packet);
                break;

            case "CHAT_RECEIVE":
                ChatManager.Instance.OnMessageReceived(packet.payload);
                break;

            case "CHAT_STATUS":
                ChatManager.Instance.UpdateChatStatus(packet.payload == "MUTED");
                break;

            case "ANSWER_RESULT":
                // Lấy tên con quái đang đụng độ
                string mId = PlayerController.LocalInstance.currentMonsterId;

                // Luôn đánh dấu là đã xong (truyền string mId vào)
                PlayerController.LocalInstance.MarkMonsterAsFinished(mId);

                // Kiểm tra nội dung tin nhắn từ Server
                string resultFromServer = packet.payload; // Đây là string (VD: "CHÍNH XÁC!")

                if (QuizUI.Instance != null)
                {
                    // TRUYỀN STRING VÀO ĐÂY (Thay vì truyền true/false)
                    QuizUI.Instance.ShowResult(resultFromServer);
                }
                break;

            case "NEW_QUESTION":
                var qData = JsonConvert.DeserializeObject<QuestionData>(packet.payload);

                if (QuizUI.Instance != null)
                {
                    QuizUI.Instance.ShowQuiz(qData, (answerIndex) =>
                    {
                        // LẤY TÊN QUÁI TỪ PLAYER CONTROLLER
                        string mId = PlayerController.LocalInstance.currentMonsterId;

                        // Gửi cả ID câu hỏi, Index trả lời và MonsterId lên Server
                        var answerPayload = new
                        {
                            questionId = qData.id,
                            answerIndex = answerIndex,
                            monsterId = mId
                        };

                        SocketClient.Instance.Send(new Packet
                        {
                            type = "ANSWER",
                            payload = JsonConvert.SerializeObject(answerPayload)
                        });
                    });
                }
                break;

            case "LEADERBOARD_UPDATE":
                if (leaderboardUI != null)
                {
                    // Hiện bảng lên nếu nó đang ẩn
                    leaderboardUI.gameObject.SetActive(true);
                    leaderboardUI.UpdateList(packet.payload);
                }
                else
                {
                    Debug.LogWarning("⚠️ Đại ca chưa kéo LeaderboardUI vào MessageHandler!");
                }
                break;

            case "SYNC_PLAYERS":
                // 1. Spawn nhân vật (giữ nguyên code cũ của đại ca)
                var list = JsonConvert.DeserializeObject<List<PlayerState>>(packet.payload);
                foreach (var state in list)
                {
                    SpawnPlayer(state.playerId, state.playerName, new Vector2(state.x, state.y));
                }

                // 2. [THÊM MỚI] Hiện tên lên Leaderboard ngay khi vào phòng
                if (LeaderboardUI.Instance != null)
                {
                    LeaderboardUI.Instance.UpdateList(packet.payload);
                }
                break;

            case "PLAYER_JOINED":
                var newState = JsonConvert.DeserializeObject<PlayerState>(packet.payload);
                SpawnPlayer(newState.playerId, newState.playerName, spawnPoint ? (Vector2)spawnPoint.position : Vector2.zero);
                break;

            case "PLAYER_LEFT":
                string leftId = packet.payload;
                if (otherPlayers.ContainsKey(leftId))
                {
                    Destroy(otherPlayers[leftId].gameObject);
                    otherPlayers.Remove(leftId);
                }
                break;

            case "PROGRESS_UPDATE":
                // Khi có cập nhật điểm số/tiến độ trong game
                if (LeaderboardUI.Instance != null)
                {
                    LeaderboardUI.Instance.UpdateList(packet.payload);
                }
                break;

            case "PLAYER_DIED":
                if (otherPlayers.ContainsKey(packet.payload))
                {
                    Destroy(otherPlayers[packet.payload].gameObject);
                    otherPlayers.Remove(packet.payload);
                }
                break;

            case "GAME_COMPLETED":
                SceneManager.LoadScene("GameEnd");
                break;
            case "GAME_OVER_SUMMARY":
                // packet.payload lúc này là JSON chứa list {name, score, time}
                if (SummaryUI.Instance != null)
                {
                    SummaryUI.Instance.DisplaySummary(packet.payload);
                }
                break;

            case "RETURN_TO_HOME":
                Debug.Log("Game kết thúc, quay về màn hình chính...");
                // Chuyển cảnh về Home
                UnityEngine.SceneManagement.SceneManager.LoadScene("Home");
                break;
        }
    }

    void SpawnPlayer(string id, string playerName, Vector2 initialPos)
    {
        if (id.StartsWith("Host_")) return;
        if (id == SocketClient.Instance.MyPlayerId && isAmHost) return;
        if (otherPlayers.ContainsKey(id)) return;

        Vector3 finalPos = (initialPos == Vector2.zero && spawnPoint != null) ? spawnPoint.position : (Vector3)initialPos;

        GameObject playerObj = Instantiate(playerPrefab, finalPos, Quaternion.identity, playersContainer);
        PlayerController pCtrl = playerObj.GetComponent<PlayerController>();

        bool isMe = (id == SocketClient.Instance.MyPlayerId);
        pCtrl.Initialize(id, isMe);

        otherPlayers.Add(id, pCtrl);
    }

    void UpdatePlayerPosition(Packet packet)
    {
        var state = JsonConvert.DeserializeObject<PlayerState>(packet.payload);

        if (otherPlayers.TryGetValue(packet.playerId, out PlayerController playerScript))
        {
            if (playerScript.IsLocal) return;
            playerScript.OnServerDataReceived(new Vector3(state.x, state.y, 0));
        }
        else
        {
            if (packet.playerId != SocketClient.Instance.MyPlayerId)
            {
                SpawnPlayer(packet.playerId, "Player", new Vector2(state.x, state.y));
            }
        }
    }

    public void OnLeaveButtonClicked()
    {
        if (SocketClient.Instance != null) SocketClient.Instance.Disconnect();
        BackToHome();
    }

    void BackToHome()
    {
        if (SocketClient.Instance) SocketClient.Instance.OnPacketReceived -= HandlePacket;
        SceneManager.LoadScene("Home");
    }
}