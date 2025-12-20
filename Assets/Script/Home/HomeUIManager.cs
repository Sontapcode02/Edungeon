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
    void Start()
    {
        ShowPanel(joinPanel);

        joinButton.onClick.AddListener(OnJoinClicked);
        confirmCreateButton.onClick.AddListener(OnConfirmHostSetup);
        backButton.onClick.AddListener(OnBackClicked);
        if (openCreatePanelButton) openCreatePanelButton.onClick.AddListener(OnCreateRoomButton);
        if (confirmCreateButton) confirmCreateButton.onClick.AddListener(OnConfirmHostSetup);
        // --- KẾT NỐI SERVER NGAY KHI VÀO GAME ĐỂ SẴN SÀNG CHECK ---
        SocketClient.Instance.ConnectOnly();

        // --- LẮNG NGHE KẾT QUẢ CHECK TỪ SERVER ---
        SocketClient.Instance.OnCheckRoomResult += HandleCheckRoomResult;
    }

    void OnDestroy()
    {
        // Nhớ hủy đăng ký khi chuyển cảnh để tránh lỗi
        if (SocketClient.Instance != null)
            SocketClient.Instance.OnCheckRoomResult -= HandleCheckRoomResult;
    }

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

        // --- GỬI LỆNH CHECK LÊN SERVER (THẬT 100%) ---
        SocketClient.Instance.SendCheckRoom(roomId);
    }

    // Hàm này tự động chạy khi Server trả lời
    void HandleCheckRoomResult(string result)
    {
        if (result == "FOUND")
        {
            Debug.Log("Phòng tồn tại -> Vào game!");

            // Lưu info
            PlayerPrefs.SetString("PLAYER_NAME", nameInput.text);
            PlayerPrefs.SetString("ROOM_ID", idRoomInput.text);
            PlayerPrefs.SetInt("IS_HOST", 0);
            PlayerPrefs.Save();

            // Chuyển cảnh
            SceneManager.LoadScene("Game"); // Đổi tên Scene cho đúng
        }
        else
        {
            Debug.Log("Không tìm thấy phòng!");
            ShowError("Phòng không tồn tại hoặc sai ID!");

            // Đợi 1 tí rồi cho quay lại nhập (dùng Invoke hoặc Coroutine)
            Invoke("BackToJoin", 2f);
        }
    }
    void OnCreateRoomButton()
    {
        string playerName = nameInput.text;
        if (string.IsNullOrEmpty(playerName))
        {
            ShowError("Đại ca nhập tên trước đã!");
            return;
        }

        // Chuyển sang màn hình Setup cho Host
        ShowPanel(hostSetupPanel);
    }
    // Hàm này gán vào nút "Xác nhận tạo" (Confirm)
    void OnConfirmHostSetup()
    {
        string questionData = questionPathInput.text; // Sau này xử lý import file
        string maxPlayers = maxPlayerInput.text;
        string playerName = nameInput.text;

        if (string.IsNullOrEmpty(maxPlayers)) maxPlayers = "4"; // Mặc định 4 người

        // Save thông tin Host
        PlayerPrefs.SetString("PLAYER_NAME", playerName);
        PlayerPrefs.SetString("ROOM_ID", GenerateRoomID()); // Tự sinh ID ngẫu nhiên
        PlayerPrefs.SetInt("IS_HOST", 1); // 1 là Host
        PlayerPrefs.SetString("MAX_PLAYERS", maxPlayers);
        PlayerPrefs.Save();
        SocketClient.Instance.ConnectOnly();
        // Host thì vào game luôn, không cần check phòng
        SceneManager.LoadScene("Game");
        
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
            // Nếu đang ở màn hình Join mà lỗi thì có thể hiện popup (hiện tại log tạm)
            Debug.LogError(msg);
        }
    }

    string GenerateRoomID()
    {
        // Random ID phòng 6 số
        return Random.Range(100000, 999999).ToString();
    }
    void BackToJoin()
    {
        ShowPanel(joinPanel);
    }
}