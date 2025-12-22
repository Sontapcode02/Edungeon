using UnityEngine;
using UnityEngine.UI;

public class HostUIManager : MonoBehaviour
{
    public Button startGameBtn;
    public Button nextQuestionBtn;

    void Start()
    {
        startGameBtn.onClick.AddListener(() => SendHostAction("START_GAME"));
        nextQuestionBtn.onClick.AddListener(() => SendHostAction("NEXT_QUESTION"));
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