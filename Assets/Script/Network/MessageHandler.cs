using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

public class MessageHandler : MonoBehaviour
{
    [Header("Host Settings")]
    public GameObject spectatorCameraPrefab;
    public GameObject hostControlUI;

    [Header("Player Settings")]
    public PlayerController playerPrefab;
    public Transform playersContainer;
    public Transform spawnPoint;

    [Header("UI")]
    public QuizUI quizUI;
    public LeaderboardUI leaderboardUI;

    private bool isAmHost = false;

    void Start()
    {
        // 1. Lấy dữ liệu từ Home gửi sang
        string myName = PlayerPrefs.GetString("PLAYER_NAME", "Unknown");
        string myRoom = PlayerPrefs.GetString("ROOM_ID", "Default");
        isAmHost = PlayerPrefs.GetInt("IS_HOST", 0) == 1;

        Debug.Log($"Game Init: {myName} | Room: {myRoom} | IsHost: {isAmHost}");

        // 2. Setup giao diện ban đầu
        SetupRoleUI();

        // 3. Kích hoạt kết nối mạng
        SocketClient.Instance.OnPacketReceived += HandlePacket;
        SocketClient.Instance.ConnectAndJoin(myName, myRoom, isAmHost);
    }

    void SetupRoleUI()
    {
        if (isAmHost)
        {
            if (spectatorCameraPrefab) Instantiate(spectatorCameraPrefab, new Vector3(0, 0, -10), Quaternion.identity);
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
            case "GAME_START":

                Debug.Log("Game Started!");

                break;
            case "PLAYER_JOINED":
                SpawnPlayer(packet.payload);
                break;

            case "ROOM_DESTROYED":
                // Server báo phòng giải tán -> Về nhà
                Debug.LogWarning("Phòng đã bị hủy: " + packet.payload);
                BackToHome();
                break;

            case "ERROR":
                Debug.LogError("Lỗi từ Server: " + packet.payload);
                BackToHome(); // Hoặc hiện popup lỗi
                break;

            case "MOVE":
                UpdatePlayerPosition(packet);
                break;

            case "ANSWER_RESULT":

                quizUI.ShowResult(packet.payload);

                break;
            case "LEADERBOARD_UPDATE":

                leaderboardUI.UpdateList(packet.payload);

                break;
            case "ITEM_EFFECT":

                //ApplyItemEffect(packet.playerId, packet.payload);

                break;
        }
    }

    void SpawnPlayer(string id)
    {
        // Nếu là ID của mình và mình là Host -> KHÔNG SPAWN
        if (id == SocketClient.Instance.MyPlayerId && isAmHost) return;

        // Spawn nhân vật
        var p = Instantiate(playerPrefab, spawnPoint.position, Quaternion.identity, playersContainer);
        bool isMe = (id == SocketClient.Instance.MyPlayerId);
        p.Initialize(id, isMe);
    }

    void UpdatePlayerPosition(Packet packet)
    {
        var pos = JsonConvert.DeserializeObject<Vector2>(packet.payload);
    }

    void BackToHome()
    {
        SocketClient.Instance.OnPacketReceived -= HandlePacket;
        SceneManager.LoadScene("Home"); // Nhớ đổi đúng tên Scene Home
    }
}