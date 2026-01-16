using System.Collections.Generic;

[System.Serializable]
public class QuizData
{
    public int id;
    public string question;
    public List<string> options;
    public int correctAnswerIndex;
    public string category;
}

[System.Serializable]
public class QuizSet
{
    public List<QuizData> questions;
}

