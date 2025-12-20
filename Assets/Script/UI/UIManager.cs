using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject quizPanel;       // Kéo cái Panel Quiz vào đây
    public GameObject leaderboardPanel; // Kéo cái Panel Leaderboard vào đây

    void Start()
    {
        // Vừa vào game là tắt nóng luôn
        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }

    // Hàm này để bật lên khi cần (gắn vào nút bấm hoặc sự kiện)
    public void ShowQuiz()
    {
        quizPanel.SetActive(true);
    }
}