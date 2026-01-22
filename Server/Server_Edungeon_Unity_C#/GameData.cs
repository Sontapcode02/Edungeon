using System;
using System.Collections.Generic;

// --- BASIC PACKETS ---
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
    public string questionsJson; // Contains question set from Host's CSV
    public int maxPlayers = 4; // [ADDED]
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

// --- PLAYER STATE (For Movement & Sync) ---
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
    public float progressPercentage; // [ADDED] To show % on Leaderboard bar
    public int score;
    public bool isAlive = true;      // [ADDED] To change row color if player fails
}