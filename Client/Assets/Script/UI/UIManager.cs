using UnityEngine;
using UnityEngine.UI;
using TMPro; // [NEW]

public class UIManager : MonoBehaviour
{
    public GameObject quizPanel;       // Kéo cái Panel Quiz vào đây
    public GameObject leaderboardPanel; // Kéo cái Panel Leaderboard vào đây

    [Header("Network Status")]
    public TMP_Text pingText; // [UPDATED] Use TMP_Text

    void Start()
    {

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);

        // Subscribe to Ping event
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.OnPingUpdate += UpdatePingUI;
        }
    }

    void OnDestroy()
    {
        if (SocketClient.Instance != null)
        {
            SocketClient.Instance.OnPingUpdate -= UpdatePingUI;
        }
    }

    void UpdatePingUI(float ping)
    {
        if (pingText != null)
        {
            pingText.text = $"Ping: {ping:F0}ms";

            // Optional: Color coding
            if (ping < 100) pingText.color = Color.green;
            else if (ping < 300) pingText.color = Color.yellow;
            else pingText.color = Color.red;
        }
    }

    // Hàm này để bật lên khi cần (gắn vào nút bấm hoặc sự kiện)
    public void ShowQuiz()
    {
        quizPanel.SetActive(true);
    }
}