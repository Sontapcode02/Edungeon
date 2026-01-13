using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class QuizUI : MonoBehaviour
{
    [Header("Quiz Panel")]
    public GameObject quizPanel;
    public TextMeshProUGUI questionText;
    public Button[] optionButtons = new Button[4];
    public TextMeshProUGUI resultText;
    public Image resultPanel;

    private bool isQuizActive = false;
    private System.Action<int> onAnswerCallback;

    void Start()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }
    }

    public void ShowQuiz(QuizData quiz, System.Action<int> callback)
    {
        if (quiz == null) return;

        onAnswerCallback = callback;
        isQuizActive = true;
        quizPanel.SetActive(true);

        questionText.text = quiz.question;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = quiz.options[i];
            optionButtons[i].interactable = true;
        }

        resultText.text = "";
        resultPanel.gameObject.SetActive(false);

        Time.timeScale = 1f; // Pause game
    }

    void OnOptionSelected(int index)
    {
        if (!isQuizActive) return;

        isQuizActive = false;

        // Disable các button
        foreach (var btn in optionButtons)
        {
            btn.interactable = false;
        }

        // Gọi callback
        onAnswerCallback?.Invoke(index);
    }

    public void ShowResult(string resultMessage)
    {
        resultPanel.gameObject.SetActive(true);
        resultText.text = resultMessage;

        // Sau 2 giây đóng quiz
        Invoke(nameof(CloseQuiz), 2f);
    }

    void CloseQuiz()
    {
        isQuizActive = false;
        quizPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}