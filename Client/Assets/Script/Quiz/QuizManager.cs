using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    private List<QuizData> quizzes = new List<QuizData>();
    private QuizData currentQuiz;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadQuizzesFromCSV(string csvContent)
    {
        quizzes.Clear();
        string[] lines = csvContent.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] parts = lines[i].Split(',');
            if (parts.Length < 6) continue;

            QuizData quiz = new QuizData
            {
                question = parts[0].Trim(),
                options = new List<string>
                {
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts[3].Trim(),
                    parts[4].Trim()
                },
                correctAnswerIndex = int.Parse(parts[5].Trim()),
                category = parts.Length > 6 ? parts[6].Trim() : "General"
            };

            quizzes.Add(quiz);
        }

        Debug.Log($"[QuizManager] Đã tải {quizzes.Count} câu hỏi từ CSV");
    }

    public QuizData GetRandomQuiz()
    {
        if (quizzes.Count == 0)
        {
            Debug.LogError("[QuizManager] Không có câu hỏi nào!");
            return null;
        }

        currentQuiz = quizzes[Random.Range(0, quizzes.Count)];
        return currentQuiz;
    }

    public bool IsAnswerCorrect(int selectedIndex)
    {
        return currentQuiz != null && selectedIndex == currentQuiz.correctAnswerIndex;
    }

    public int GetTotalQuizCount()
    {
        return quizzes.Count;
    }
}