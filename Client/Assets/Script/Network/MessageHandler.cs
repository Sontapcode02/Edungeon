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
    public GameObject[] playerPrefabs; // Array of prefabs for random selection
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
                if (spawnPoint == null) Debug.LogWarning("⚠️ MessageHandler: spawnPoint is NULL! Using default (0,0,-10).");
                else Debug.Log($"✅ MessageHandler: Spawning Host at spawnPoint: {spawnPoint.position}");

                Vector3 spawnPos = (spawnPoint != null) ? spawnPoint.position : new Vector3(0, 0, -10);
                Instantiate(spectatorCameraPrefab, spawnPos, Quaternion.identity);
            }
            if (hostControlUI) hostControlUI.SetActive(true);
        }
        else
        {
            if (hostControlUI) hostControlUI.SetActive(false);
        }
    }

    // --- PACKET HANDLING FUNCTION (JSON ERROR FIXED) ---
    void HandlePacket(Packet packet)
    {
        switch (packet.type)
        {
            // [IMPORTANT] These cases return simple strings (Success/FOUND)
            // DO NOT use JsonConvert.DeserializeObject here!

            case "CHECK_ROOM_RESPONSE":
                // Payload is "FOUND" or "NOT_FOUND" -> Use directly
                SocketClient.Instance.OnCheckRoomResult?.Invoke(packet.payload);
                break;

            case "ROOM_CREATED":
                // Payload is "Success" -> Use directly, don't Deserialize!
                string hostId = packet.playerId;
                SocketClient.Instance.MyPlayerId = hostId;

                // Notify HomeUIManager of success
                SocketClient.Instance.OnCreateRoomResult?.Invoke("SUCCESS");
                break;

            case "JOIN_SUCCESS":
                // Payload is "Success"
                string myId = packet.playerId;
                SocketClient.Instance.MyPlayerId = myId;
                // Get my name from PlayerPrefs since JOIN_SUCCESS might not have it or it's just 'Success'
                string myName = PlayerPrefs.GetString("PLAYER_NAME", "Me");

                // Handle character spawning
                if (otherPlayers.ContainsKey(myId))
                {
                    PlayerController myPlayerScript = otherPlayers[myId];
                    myPlayerScript.Initialize(myId, myName, true);
                    otherPlayers.Remove(myId);
                }
                else
                {
                    SpawnPlayer(myId, myName, Vector2.zero);
                }
                break;
            // -----------------------------------------------------------

            case "ERROR":
                string errorMsg = packet.payload;
                if (errorMsg.Contains("defeated"))
                {
                    Debug.Log($"<color=cyan>[System]</color>: {errorMsg}");
                    // If you have Toast or Popup script, call it here
                    // UIHint.Show(errorMsg); 

                    // DESTROY the "ghost" monster on the screen
                    GameObject monster = GameObject.Find(PlayerController.LocalInstance.currentMonsterId);
                    if (monster != null) monster.SetActive(false);
                }
                else
                {
                    Debug.LogError("Server Error: " + errorMsg);
                }
                break;

            case "START_GAME":
                Debug.Log("Game Start!");
                break;
            case "GAME_PAUSED":
                Debug.Log("<color=yellow>⚠️ GAME PAUSED!</color>");

                // 1. Lock player movement
                if (PlayerController.LocalInstance != null)
                    PlayerController.LocalInstance.isPaused = true;

                // 2. Show UI Notification
                string pauseMsg = packet.payload;
                if (NotificationUI.Instance != null)
                    NotificationUI.Instance.ShowMessage(pauseMsg, false);
                break;

            case "GAME_RESUMED":
                Debug.Log("<color=green>▶️ RESUME RACING!</color>");

                // 1. Unlock movement
                if (PlayerController.LocalInstance != null)
                    PlayerController.LocalInstance.isPaused = false;

                // 2. Hide Notification
                if (NotificationUI.Instance != null)
                    NotificationUI.Instance.HideMessage();
                break;

            case "OPEN_GATE":
                Debug.Log("<color=cyan>🔔 Game Starting! Countdown...</color>");
                if (enemyContainer != null)
                {
                    enemyContainer.SetActive(true);
                }
                // 1. Ensure player is locked (isPaused = true)
                if (PlayerController.LocalInstance != null)
                    PlayerController.LocalInstance.isPaused = true;

                // 2. Start countdown 3 seconds
                if (NotificationUI.Instance != null)
                {
                    NotificationUI.Instance.StartCountdown(3, () =>
                    {
                        if (lobbyGate != null)
                        {
                            lobbyGate.SetActive(false);
                            // gateObject.GetComponent<Collider2D>().enabled = false; 
                            Debug.Log("<color=yellow>🚪 GATE OPEN!</color>");
                        }
                        // 3. Unpause when finished
                        if (PlayerController.LocalInstance != null)
                            PlayerController.LocalInstance.isPaused = false;

                        Debug.Log("<color=green>🔥 START!</color>");
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
                // Get current monster name
                string mId = PlayerController.LocalInstance.currentMonsterId;

                // Mark monster as finished
                PlayerController.LocalInstance.MarkMonsterAsFinished(mId);

                // Check message from Server
                string resultFromServer = packet.payload; // e.g. "CORRECT!"

                if (QuizUI.Instance != null)
                {
                    // PASS STRING HERE
                    QuizUI.Instance.ShowResult(resultFromServer);
                }
                break;

            case "NEW_QUESTION":
                var qData = JsonConvert.DeserializeObject<QuestionData>(packet.payload);

                if (QuizUI.Instance != null)
                {
                    QuizUI.Instance.ShowQuiz(qData, (answerIndex) =>
                    {
                        // GET MONSTER FROM PLAYER CONTROLLER
                        string mId = PlayerController.LocalInstance.currentMonsterId;

                        // Send question ID, Answer Index and Monster ID to Server
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
                    // Show board if hidden
                    leaderboardUI.gameObject.SetActive(true);
                    leaderboardUI.UpdateList(packet.payload);
                }
                else
                {
                    Debug.LogWarning("⚠️ LeaderboardUI not assigned in MessageHandler!");
                }
                break;

            case "SYNC_PLAYERS":
                // 1. Spawn players
                var list = JsonConvert.DeserializeObject<List<PlayerState>>(packet.payload);
                foreach (var state in list)
                {
                    // [FIX] Skip my own data (handled by JOIN_SUCCESS)
                    if (state.playerId == SocketClient.Instance.MyPlayerId) continue;

                    // [FIX] If player exists, update position! 
                    // This fixes the bug where Late Joiners see existing players at (0,0)
                    if (otherPlayers.ContainsKey(state.playerId))
                    {
                        // [FIX] Don't move Local Player! Local Player controls their own position.
                        PlayerController p = otherPlayers[state.playerId];
                        if (p.IsLocal) continue;

                        // Snap to position immediately
                        p.transform.position = new Vector3(state.x, state.y, 0);
                    }
                    else
                    {
                        SpawnPlayer(state.playerId, state.playerName, new Vector2(state.x, state.y));
                    }
                }

                // 2. [NEW] Show names on Leaderboard immediately on join
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
                // On score/progress update
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
                // packet.payload is JSON list {name, score, time}
                if (SummaryUI.Instance != null)
                {
                    SummaryUI.Instance.DisplaySummary(packet.payload);
                }
                break;

            case "RETURN_TO_HOME":
                Debug.Log("Game Ended, returning to Home...");
                // Load Home scene
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

        // --- RANDOM PLAYER MODEL SELECTION ---
        GameObject selectedPrefab = null;
        if (playerPrefabs != null && playerPrefabs.Length > 0)
        {
            // Deterministic random based on ID hash so all clients see the same model for this ID
            int index = Mathf.Abs(id.GetHashCode()) % playerPrefabs.Length;
            selectedPrefab = playerPrefabs[index];
        }

        if (selectedPrefab == null)
        {
            Debug.LogError("No Player Prefab assigned in MessageHandler!");
            return;
        }

        GameObject playerObj = Instantiate(selectedPrefab, finalPos, Quaternion.identity, playersContainer);
        PlayerController pCtrl = playerObj.GetComponent<PlayerController>();

        bool isMe = (id == SocketClient.Instance.MyPlayerId);
        pCtrl.Initialize(id, playerName, isMe);

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