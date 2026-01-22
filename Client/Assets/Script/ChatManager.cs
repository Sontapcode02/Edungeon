using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance;

    [Header("UI References")]
    public GameObject chatBox;          // Drag cái panel chứa chat vào đây
    public Button chatOpenBtn;          // Nút icon để mở chat
    public Button chatCloseBtn;         // Nút X để đóng chat

    public ScrollRect chatScrollRect;
    public TMP_InputField chatInput;
    public Button sendBtn;
    public TextMeshProUGUI chatContentText; // Kéo cái Text trong ScrollView vào đây
    public Toggle muteToggle;    // Chỉ hiện cho Host

    private bool isHost = false; // Đại ca set biến này khi vào phòng (dựa vào response CREATE_ROOM)

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // --- Bật/Tắt Chat Box ---
        if (chatOpenBtn != null)
            chatOpenBtn.onClick.AddListener(OpenChatBox);

        if (chatCloseBtn != null)
            chatCloseBtn.onClick.AddListener(CloseChatBox);

        // --- Gửi Tin Nhắn ---
        sendBtn.onClick.AddListener(SendChatMessage);

        // Lắng nghe sự kiện End Edit của InputField (khi nhấn Enter)
        chatInput.onEndEdit.AddListener(OnInputEndEdit);

        // Host mới nhìn thấy nút này
        if (isHost)
        {
            muteToggle.gameObject.SetActive(true);
            muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
        }
        else
        {
            muteToggle.gameObject.SetActive(false);
        }

        // --- Mặc định Chat Box Bị Ẩn ---
        if (chatBox != null)
            chatBox.SetActive(false);

        // [FIX] Force default text color to black
        if (chatContentText != null)
            chatContentText.color = Color.black;
    }

    // --- MỞ CHAT BOX ---
    void OpenChatBox()
    {
        if (chatBox != null)
        {
            chatBox.SetActive(true);
            chatInput.ActivateInputField(); // Tự động focus vào input field
            Debug.Log(">>> Chat box mở!");
        }
    }

    // --- ĐÓNG CHAT BOX ---
    void CloseChatBox()
    {
        if (chatBox != null)
        {
            chatBox.SetActive(false);
            Debug.Log(">>> Chat box đóng!");
        }
    }

    // 1. Gửi tin nhắn
    void SendChatMessage()
    {
        if (string.IsNullOrEmpty(chatInput.text)) return;

        SocketClient.Instance.Send(new Packet
        {
            type = "CHAT_MESSAGE",
            payload = chatInput.text
        });

        chatInput.text = ""; // Xóa ô nhập sau khi gửi
    }

    // 1.5. Xử lý sự kiện End Edit (nhấn Enter)
    void OnInputEndEdit(string text)
    {
        // Chỉ gửi khi người dùng nhấn Enter (không phải khi submit được gọi từ nơi khác)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendChatMessage();
            // Giữ focus ở input field để người dùng có thể tiếp tục nhập
            chatInput.ActivateInputField();
        }
    }

    // 2. Host bật/tắt chat
    void OnMuteToggleChanged(bool isMuted)
    {
        string action = isMuted ? "MUTE_CHAT" : "UNMUTE_CHAT";
        SocketClient.Instance.Send(new Packet
        {
            type = "HOST_ACTION",
            payload = action
        });
    }

    // 3. Nhận tin nhắn từ Server (Gọi từ NetworkManager/SocketClient)
    public void OnMessageReceived(string message)
    {
        // [FIX] Force black color for every message
        string coloredMessage = $"<color=#000000>{message}</color>";
        chatContentText.text += coloredMessage + "\n";

        StartCoroutine(ScrollToBottom());
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        // [FIX] Force rebuild layout to ensure text height is calculated correctly
        LayoutRebuilder.ForceRebuildLayoutImmediate(chatScrollRect.content);

        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }

    // 4. Nhận trạng thái Mute (để khóa ô nhập của member)
    public void UpdateChatStatus(bool isMuted)
    {
        if (isHost) return; // Host thì không bao giờ bị khóa mồm

        chatInput.interactable = !isMuted;
        sendBtn.interactable = !isMuted;

        if (isMuted)
        {
            chatInput.placeholder.GetComponent<Text>().text = "Chat muted...";
        }
        else
        {
            chatInput.placeholder.GetComponent<Text>().text = "Enter message...";
        }
    }
}