using System.Collections.Generic;

[System.Serializable]
public class QuizData
{
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

[System.Serializable]
public class PlayerProgress
{
    public string playerId;
    public string playerName;
    public int currentCheckpoint;
    public float progressPercentage;
    public int score;
    public bool isAlive;
}