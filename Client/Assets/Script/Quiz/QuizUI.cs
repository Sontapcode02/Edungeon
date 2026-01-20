using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class QuizUI : MonoBehaviour
{
    // 1. Singleton để các script khác (như MessageHandler) gọi dễ dàng
    public static QuizUI Instance;

    [Header("UI References")]
    public GameObject quizPanel;          // Kéo Quiz_banner vào đây
    public TextMeshProUGUI questionText; // Kéo Text câu hỏi vào đây
    public Button[] optionButtons = new Button[4]; // Danh sách 4 nút đáp án
    public TextMeshProUGUI resultText;   // Text báo Đúng/Sai
    public GameObject resultPanel;            // Panel chứa text kết quả

    private bool isQuizActive = false;
    private System.Action<int> onAnswerCallback;

    void Awake()
    {
        // Khởi tạo Singleton ngay khi game chạy
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // Đảm bảo Banner ẩn lúc mới vào game
        if (quizPanel != null) quizPanel.SetActive(false);
    }

    void Start()
    {
        // Gán sự kiện cho 4 nút bấm
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null)
            {
                int index = i;
                optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
                // --- AUDIO ---
                optionButtons[i].onClick.AddListener(() =>
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayClickSound();
                });
            }
        }
    }

    /// <summary>
    /// Hiển thị bảng câu hỏi lên màn hình
    /// </summary>
    public void ShowQuiz(QuestionData quiz, System.Action<int> callback)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false); // Ẩn cái bảng kết quả đi
        }
        if (resultText != null)
        {
            resultText.text = ""; // Xóa chữ "Chính xác/Sai rồi" cũ đi cho sạch
        }
        // Log kiểm tra xem hàm có thực sự được gọi không
        Debug.Log($"🔍 [CHECK] Hàm ShowQuiz ĐÃ CHẠY từ Object: {gameObject.name}", gameObject);

        if (quiz == null) return;

        // KIỂM TRA AN TOÀN: Nếu chưa kéo dây trong Inspector sẽ báo lỗi ngay
        if (quizPanel == null)
        {
            Debug.LogError($"❌ [QuizUI] Thằng {gameObject.name} bị thiếu Quiz Panel! Kiểm tra lại Inspector ngay.", this);
            return;
        }

        onAnswerCallback = callback;
        isQuizActive = true;

        // 1. Đánh thức Object này dậy (nếu nó đang bị tắt ở Hierarchy)
        this.gameObject.SetActive(true);

        // 2. Hiện Banner và đổ dữ liệu
        quizPanel.SetActive(true);
        questionText.text = quiz.question;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null && i < quiz.options.Count)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = quiz.options[i];
                optionButtons[i].interactable = true;
            }
            else if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }

        if (resultPanel) resultPanel.gameObject.SetActive(false);

        // 3. Tạm dừng game để tập trung trả lời câu hỏi
        Time.timeScale = 0f;

        Debug.Log($"✅ [UI CHECK] Panel đang hiển thị: {quizPanel.activeSelf} | TimeScale: {Time.timeScale}");
    }

    void OnOptionSelected(int index)
    {
        if (!isQuizActive) return;
        isQuizActive = false;

        // Khóa các nút lại để tránh spam click
        foreach (var btn in optionButtons)
        {
            if (btn != null) btn.interactable = false;
        }

        // Gửi kết quả về MessageHandler để báo lên Server
        onAnswerCallback?.Invoke(index);
    }

    [Header("Audio")]
    public AudioClip correctSFX;
    public AudioClip wrongSFX;

    /// <summary>
    /// Hiển thị thông báo Đúng/Sai từ Server gửi về
    /// </summary>
    public void ShowResult(string resultMessage)
    {
        if (resultPanel) resultPanel.gameObject.SetActive(true);
        if (resultText) resultText.text = resultMessage;

        // --- AUDIO ---
        if (AudioManager.Instance != null)
        {
            if (resultMessage.Contains("Đúng") || resultMessage.ToLower().Contains("correct"))
            {
                AudioManager.Instance.PlaySFX(correctSFX);
            }
            else
            {
                AudioManager.Instance.PlaySFX(wrongSFX);
            }
        }

        // Đóng Quiz sau 1.5 giây (Dùng WaitForSecondsRealtime vì game đang Pause)
        StartCoroutine(CloseQuizAfterDelay(1.5f));
    }

    private IEnumerator CloseQuizAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        CloseQuiz();
    }

    void CloseQuiz()
    {
        isQuizActive = false;
        if (quizPanel) quizPanel.SetActive(false);

        // Cho game chạy tiếp tục
        Time.timeScale = 1f;
        Debug.Log(">>> Quiz đã đóng, game tiếp tục!");
    }
}