using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class QuizUI : MonoBehaviour
{
    // 1. Singleton for easy access from other scripts
    public static QuizUI Instance;

    [Header("UI References")]
    public GameObject quizPanel;          // Drag Quiz_banner here
    public TextMeshProUGUI questionText; // Drag Question Text here
    public Button[] optionButtons = new Button[4]; // List of 4 option buttons
    public TextMeshProUGUI resultText;   // Correct/Wrong text
    public GameObject resultPanel;            // Panel containing result text

    private bool isQuizActive = false;
    private System.Action<int> onAnswerCallback;

    void Awake()
    {
        // Init Singleton immediately
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // Ensure Banner is hidden on start
        if (quizPanel != null) quizPanel.SetActive(false);
    }

    void Start()
    {
        // Assign events to 4 buttons
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
    /// Show quiz panel on screen
    /// </summary>
    public void ShowQuiz(QuestionData quiz, System.Action<int> callback)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false); // Hide result panel
        }
        if (resultText != null)
        {
            resultText.text = ""; // Clear old Correct/Wrong text
        }
        // Check if function is running
        Debug.Log($"🔍 [CHECK] ShowQuiz RUNNING from Object: {gameObject.name}", gameObject);

        if (quiz == null) return;

        // SAFETY CHECK: If not assigned in Inspector, log error
        if (quizPanel == null)
        {
            Debug.LogError($"❌ [QuizUI] {gameObject.name} missing Quiz Panel! Check Inspector.", this);
            return;
        }

        onAnswerCallback = callback;
        isQuizActive = true;

        // 1. Activate this Object (if disabled in Hierarchy)
        this.gameObject.SetActive(true);

        // 2. Show Banner and fill data
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

        // 3. Pause game to focus on answering
        Time.timeScale = 0f;

        Debug.Log($"✅ [UI CHECK] Panel showing: {quizPanel.activeSelf} | TimeScale: {Time.timeScale}");
    }

    void OnOptionSelected(int index)
    {
        if (!isQuizActive) return;
        isQuizActive = false;

        // Lock buttons to avoid spam click
        foreach (var btn in optionButtons)
        {
            if (btn != null) btn.interactable = false;
        }

        // Send result to MessageHandler to report to Server
        onAnswerCallback?.Invoke(index);
    }

    [Header("Audio")]
    public AudioClip correctSFX;
    public AudioClip wrongSFX;

    /// <summary>
    /// Show Correct/Wrong notification from Server
    /// </summary>
    public void ShowResult(string resultMessage)
    {
        if (resultPanel) resultPanel.gameObject.SetActive(true);
        if (resultText) resultText.text = resultMessage;

        // --- AUDIO ---
        if (AudioManager.Instance != null)
        {
            // [FIXED] Updated to match English Server message
            if (resultMessage.ToUpper().Contains("CORRECT"))
            {
                AudioManager.Instance.PlaySFX(correctSFX);
            }
            else
            {
                AudioManager.Instance.PlaySFX(wrongSFX);
            }
        }

        // Close Quiz after 1.5s (Use WaitForSecondsRealtime because game is Paused)
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

        // Resume game
        Time.timeScale = 1f;
        Debug.Log(">>> Quiz closed, game resumed!");
    }
}