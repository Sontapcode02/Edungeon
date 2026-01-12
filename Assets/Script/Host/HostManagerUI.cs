using UnityEngine;
using UnityEngine.UI;

public class HostUIManager : MonoBehaviour
{
    public Button startGameBtn;
    // Bỏ nút nextQuestionBtn

    void Start()
    {
        // Chỉ xử lý sự kiện Start Game
        startGameBtn.onClick.AddListener(() => SendHostAction("START_GAME"));
    }

    void SendHostAction(string actionName)
    {
        // Gửi gói tin HOST_ACTION lên Server
        SocketClient.Instance.Send(new Packet
        {
            type = "HOST_ACTION",
            payload = actionName
        });
    }
}