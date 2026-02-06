using System;
using System.Collections.Generic;

// --- BASIC PACKET ---
[System.Serializable]
public class Packet
{
    public string type;
    public string playerId;
    public string payload;
}

[System.Serializable]
public class HandshakeData
{
    public string playerName;
    public string roomId;
    public string questionsJson; // Contains questions from Host CSV
    public int maxPlayers = 4; // [ADDED]
    public string captchaToken; // [SECURITY] Turnstile Token
}

// --- QUESTION DATA ---
[System.Serializable]
public class QuestionData
{
    public int id;
    public string question;
    public List<string> options;
    public int correctIndex;
    public string category;
    public int timeLimit = 15;
}

// --- PLAYER STATE (For movement & Sync) ---
[System.Serializable]
public class PlayerState
{
    public string playerId;
    public string playerName;
    public float x;
    public float y;
    public int score;
    public bool isReady;
}

// --- GAME PROGRESS (For Leaderboard) ---
[System.Serializable]
public class PlayerProgress
{
    public string playerId;
    public string playerName;
    public float progressPercentage; // [NEW] To show % on Leaderboard
    public int score;
    public bool isAlive = true;      // [NEW] To change color if player dies
}