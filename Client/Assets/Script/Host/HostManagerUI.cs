using UnityEngine;
using UnityEngine.UI;
using TMPro; // Đảm bảo đại ca đã cài TextMeshPro

public class HostUIManager : MonoBehaviour
{
    [Header("UI Components")]
    public Button actionBtn;           // Nút bấm chính (Start/Pause/Resume)
    public TextMeshProUGUI btnText;    // Text hiển thị trên nút

    [Header("Settings")]
    public Color startColor = Color.green;
    public Color pauseColor = Color.yellow;
    public Color resumeColor = Color.cyan;

    // Các trạng thái của Game
    private enum GameState { Ready, Playing, Paused }
    private GameState currentState = GameState.Ready;

    void Start()
    {
        if (actionBtn != null)
        {
            // Gán sự kiện Click cho nút
            actionBtn.onClick.AddListener(HandleButtonClick);

            // Khởi tạo trạng thái ban đầu cho nút
            UpdateBtnUI("START", startColor);
        }
    }

    private void HandleButtonClick()
    {
        switch (currentState)
        {
            case GameState.Ready:
                // --- TRẠNG THÁI: CHUẨN BỊ BẮT ĐẦU ---
                SendHostAction("START_GAME");
                UpdateBtnUI("PAUSE", pauseColor);
                currentState = GameState.Playing;
                Debug.Log("<color=green>[HOST]</color> Game Started!");
                break;

            case GameState.Playing:
                // --- TRẠNG THÁI: ĐANG CHƠI -> BẤM ĐỂ TẠM DỪNG ---
                SendHostAction("PAUSE_GAME");
                UpdateBtnUI("RESUME", resumeColor);
                currentState = GameState.Paused;
                Debug.Log("<color=yellow>[HOST]</color> Game Paused!");
                break;

            case GameState.Paused:
                // --- TRẠNG THÁI: ĐANG DỪNG -> BẤM ĐỂ CHƠI TIẾP ---
                SendHostAction("RESUME_GAME");
                UpdateBtnUI("PAUSE", pauseColor);
                currentState = GameState.Playing;
                Debug.Log("<color=cyan>[HOST]</color> Game Resumed!");
                break;
        }
    }

    // Hàm cập nhật giao diện nút bấm
    private void UpdateBtnUI(string text, Color color)
    {
        if (btnText != null) btnText.text = text;

        // Đổi màu nền nút cho đại ca dễ nhìn
        Image btnImg = actionBtn.GetComponent<Image>();
        if (btnImg != null) btnImg.color = color;
    }

    // Gửi gói tin lên Server
    private void SendHostAction(string actionName)
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.Send(new Packet
            {
                type = "HOST_ACTION",
                payload = actionName
            });
        }
        else
        {
            Debug.LogError("SocketClient chưa được khởi tạo đại ca ơi!");
        }
    }
}