using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class HomeUIManager : MonoBehaviour
{
    [Header("--- PANEL ---")]
    public GameObject joinPanel;
    public GameObject loadingPanel;
    public GameObject hostSetupPanel;

    [Header("--- JOIN ROOM UI ---")]
    public TMP_InputField nameInput;
    public TMP_InputField idRoomInput;
    public Button joinButton;
    public Button openCreatePanelButton;

    [Header("--- LOADING UI ---")]
    public TextMeshProUGUI statusText;

    [Header("--- HOST SETUP UI ---")]
    public TMP_InputField questionPathInput;
    public TMP_InputField maxPlayerInput;
    public Button confirmCreateButton;
    public Button backButton;

    // Biến lưu tạm để dùng khi Server phản hồi thành công
    private string tempHostName;
    private string tempRoomId;

    void Start()
    {
        ShowPanel(joinPanel);

        joinButton.onClick.AddListener(OnJoinClicked);
        confirmCreateButton.onClick.RemoveListener(OnConfirmHostSetup);
        confirmCreateButton.onClick.AddListener(OnConfirmHostSetup);
        backButton.onClick.AddListener(OnBackClicked);
        if (openCreatePanelButton) openCreatePanelButton.onClick.AddListener(OnCreateRoomButton);

        // 1. Kết nối Server ngay khi mở game
        SocketClient.Instance.ConnectOnly();

        // 2. Lắng nghe các sự kiện từ Server trả về
        SocketClient.Instance.OnCheckRoomResult += HandleCheckRoomResult;
        SocketClient.Instance.OnCreateRoomResult -= HandleCreateRoomResult;
        SocketClient.Instance.OnCreateRoomResult += HandleCreateRoomResult; // <--- Cần thêm sự kiện này bên SocketClient
    }

    void OnDestroy()
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.OnCheckRoomResult -= HandleCheckRoomResult;
            SocketClient.Instance.OnCreateRoomResult -= HandleCreateRoomResult;
        }
    }

    // --- PHẦN KHÁCH (JOIN) ---
    void OnJoinClicked()
    {
        string playerName = nameInput.text;
        string roomId = idRoomInput.text;

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(roomId))
        {
            ShowError("Nhập đủ tên và ID phòng đi đại ca!");
            return;
        }

        // Hiện Loading
        ShowPanel(loadingPanel);
        statusText.text = "Đang kiểm tra phòng...";
        statusText.color = Color.yellow;

        // Gửi lệnh check
        SocketClient.Instance.SendCheckRoom(roomId);
    }

    void HandleCheckRoomResult(string result)
    {
        if (result == "FOUND")
        {
            Debug.Log("Phòng tồn tại -> Vào game!");

            // Lưu info
            PlayerPrefs.SetString("PLAYER_NAME", nameInput.text);
            PlayerPrefs.SetString("ROOM_ID", idRoomInput.text);
            PlayerPrefs.SetInt("IS_HOST", 0); // Khách
            PlayerPrefs.Save();

            SceneManager.LoadScene("Game");
        }
        else
        {
            Debug.Log("Không tìm thấy phòng!");
            ShowError("Phòng không tồn tại hoặc sai ID!");
            Invoke("BackToJoin", 2f);
        }
    }

    // --- PHẦN CHỦ PHÒNG (HOST) ---
    void OnCreateRoomButton()
    {
        string playerName = nameInput.text;
        if (string.IsNullOrEmpty(playerName))
        {
            ShowError("Đại ca nhập tên trước đã!");
            return;
        }
        // Lưu tạm tên
        tempHostName = playerName;

        // Chuyển sang màn hình Setup
        ShowPanel(hostSetupPanel);
    }

    // Nút xác nhận tạo phòng
    void OnConfirmHostSetup()
    {
        // 1. Chuẩn bị dữ liệu
        confirmCreateButton.interactable = false;
        string roomId = GenerateRoomID();
        string maxPlayers = maxPlayerInput.text;
        if (string.IsNullOrEmpty(maxPlayers)) maxPlayers = "4";

        tempRoomId = roomId; // Lưu tạm ID

        // 2. Hiện Loading chờ Server
        ShowPanel(loadingPanel);
        statusText.text = "Đang tạo phòng...";
        statusText.color = Color.yellow;

        // 3. Gửi lệnh TẠO PHÒNG lên Server (Thay vì vào game luôn)
        HandshakeData data = new HandshakeData();
        data.roomId = roomId;
        data.playerName = tempHostName;

        SocketClient.Instance.Send(new Packet
        {
            type = "CREATE_ROOM",
            payload = JsonUtility.ToJson(data)
        });
    }

    // Hàm xử lý khi Server báo "Tạo thành công"
    void HandleCreateRoomResult(string result)
    {
        confirmCreateButton.interactable = true;
        if (result == "SUCCESS")
        {
            // Lưu Prefs
            PlayerPrefs.SetString("PLAYER_NAME", tempHostName);
            PlayerPrefs.SetString("ROOM_ID", tempRoomId);
            PlayerPrefs.SetInt("IS_HOST", 1); // Host
            PlayerPrefs.SetString("MAX_PLAYERS", maxPlayerInput.text);
            PlayerPrefs.Save();

            // Lúc này mới chuyển cảnh
            SceneManager.LoadScene("Game");
        }
        else
        {
            ShowError("Tạo phòng thất bại: " + result);
            Invoke("BackToJoin", 2f);
        }
    }

    // --- UTILS ---
    void OnBackClicked()
    {
        ShowPanel(joinPanel);
    }

    void ShowPanel(GameObject panelToShow)
    {
        joinPanel.SetActive(false);
        loadingPanel.SetActive(false);
        hostSetupPanel.SetActive(false);
        panelToShow.SetActive(true);
    }

    void ShowError(string msg)
    {
        if (loadingPanel.activeSelf)
        {
            statusText.text = msg;
            statusText.color = Color.red;
        }
        else
        {
            Debug.LogError(msg);
        }
    }

    string GenerateRoomID()
    {
        return Random.Range(100000, 999999).ToString();
    }

    void BackToJoin()
    {
        ShowPanel(joinPanel);
    }
}