using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Runtime.InteropServices; // [NEW] For DllImport


// Chỉ dùng thư viện này trong Unity Editor để mở cửa sổ chọn file
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("--- NEW FEATURE ---")]
    public Button browseButton; // <--- Kéo nút "Chọn File" vào đây

    // Temp variables
    private string tempHostName;
    private string tempRoomId;
    private string loadedCsvContent; // [NEW] Store content from WebGL upload

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName, string filter);
#endif

    void Start()
    {
        ShowPanel(joinPanel);

        // --- Gán sự kiện cho các nút ---
        joinButton.onClick.AddListener(OnJoinClicked);

        confirmCreateButton.onClick.RemoveListener(OnConfirmHostSetup);
        confirmCreateButton.onClick.AddListener(OnConfirmHostSetup);

        backButton.onClick.AddListener(OnBackClicked);

        if (openCreatePanelButton)
            openCreatePanelButton.onClick.AddListener(OnCreateRoomButton);

        // Gán sự kiện cho nút Chọn File (Browse)
        if (browseButton != null)
        {
            browseButton.onClick.AddListener(OnBrowseFileClicked);
        }

        // 1. Kết nối Server ngay khi mở game
        SocketClient.Instance.ConnectOnly();

        // 2. Lắng nghe các sự kiện từ Server trả về
        SocketClient.Instance.OnCheckRoomResult += HandleCheckRoomResult;
        SocketClient.Instance.OnCreateRoomResult -= HandleCreateRoomResult;
        SocketClient.Instance.OnCreateRoomResult += HandleCreateRoomResult;
    }

    void OnDestroy()
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.OnCheckRoomResult -= HandleCheckRoomResult;
            SocketClient.Instance.OnCreateRoomResult -= HandleCreateRoomResult;
        }
    }

    // --- FILE BROWSE FEATURE ---
    void OnBrowseFileClicked()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Select Question File (CSV)", "", "csv");
        if (!string.IsNullOrEmpty(path))
        {
            questionPathInput.text = path;
            Debug.Log("✅ File selected: " + path);
            loadedCsvContent = null; // Reset web content if used in Editor
        }
#elif UNITY_WEBGL
        // Call JS Plugin
        // gameObjectName = "HomeUIManager" (Check GameObject name in Scene!)
        // methodName = "OnFileUploaded"
        // filter = ".csv"
        UploadFile(gameObject.name, "OnFileUploaded", ".csv");
#endif
    }

    // [NEW] Callback from WebGL
    public void OnFileUploaded(string content)
    {
        loadedCsvContent = content;
        questionPathInput.text = "File Loaded from WebGL"; // Fake path for UI
        Debug.Log("✅ CSV Content received from WebGL!");
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
        // 1. Khóa nút để tránh bấm nhiều lần
        confirmCreateButton.interactable = false;

        string roomId = GenerateRoomID();
        string maxPlayers = maxPlayerInput.text;
        if (string.IsNullOrEmpty(maxPlayers)) maxPlayers = "4";

        // --- STEP 1: GET FILE PATH (FOR EDITOR/DESKTOP) --- 
        string quizFilePath = questionPathInput.text;
        if (string.IsNullOrEmpty(quizFilePath)) quizFilePath = @"D:\questions.csv"; // Default

        // --- STEP 2: READ FILE CONTENT ---
        List<QuestionData> processedQuestions = new List<QuestionData>();

        try
        {
            string[] lines;

            // Priority: WebGL Content -> Local File
            if (!string.IsNullOrEmpty(loadedCsvContent))
            {
                // Split by newline (handle various line endings)
                lines = loadedCsvContent.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            }
            else if (File.Exists(quizFilePath))
            {
                lines = File.ReadAllLines(quizFilePath);
            }
            else
            {
                ShowError("❌ File not found!");
                confirmCreateButton.interactable = true;
                return;
            }

            // Kiểm tra nếu file quá ngắn (chỉ có header hoặc trống trơn)
            if (lines.Length <= 1)
            {
                ShowError("❌ File trống rỗng! Đại ca kiểm tra lại nội dung.");
                confirmCreateButton.interactable = true;
                return; // ⛔ DỪNG NGAY
            }

            // Bắt đầu từ i=1 để bỏ qua dòng tiêu đề
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] parts = line.Split(','); // Tách bằng dấu gạch đứng

                // Kiểm tra đủ cột không (Ít nhất phải có Câu hỏi + 4 Đáp án + Đáp án đúng = 6 cột)
                if (parts.Length < 6)
                {
                    // Log cảnh báo dòng lỗi (nhưng không dừng, bỏ qua dòng này đọc dòng tiếp)
                    Debug.LogWarning($"⚠️ Dòng {i + 1} sai định dạng: {line}");
                    continue;
                }

                QuestionData q = new QuestionData();
                q.id = i;
                q.question = parts[0];

                // Chỗ này hôm qua đại ca sửa List rồi nè
                q.options = new List<string> { parts[1], parts[2], parts[3], parts[4] };

                if (int.TryParse(parts[5], out int correctIdx))
                {
                    q.correctIndex = correctIdx;
                }
                else
                {
                    Debug.LogWarning($"⚠️ Dòng {i + 1}: Đáp án đúng không phải là số!");
                    continue;
                }

                if (parts.Length > 6) q.category = parts[6];

                processedQuestions.Add(q);
            }
        }
        catch (System.Exception ex)
        {
            ShowError("❌ Lỗi khi đọc file: " + ex.Message);
            confirmCreateButton.interactable = true;
            return; // ⛔ DỪNG NGAY
        }

        // --- BƯỚC 3: KIỂM TRA SỐ LƯỢNG CÂU HỎI HỢP LỆ ---
        if (processedQuestions.Count == 0)
        {
            ShowError("❌ File không có câu nào hợp lệ! (Kiểm tra xem có dùng dấu '|' không?)");
            confirmCreateButton.interactable = true;
            return; // ⛔ DỪNG NGAY: Không gửi gói tin rỗng
        }

        // --- BƯỚC 4: MỌI THỨ NGON LÀNH -> GỬI SERVER ---
        Debug.Log($"✅ Duyệt thành công {processedQuestions.Count} câu hỏi -> Đang gửi Server...");

        tempRoomId = roomId;
        ShowPanel(loadingPanel);
        statusText.text = "Đang tải dữ liệu lên Server...";
        statusText.color = Color.yellow;

        HandshakeData data = new HandshakeData
        {
            roomId = roomId,
            playerName = tempHostName,
            questionsJson = JsonConvert.SerializeObject(processedQuestions) // Đóng gói gửi đi
        };

        SocketClient.Instance.Send(new Packet
        {
            type = "CREATE_ROOM",
            payload = JsonConvert.SerializeObject(data)
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