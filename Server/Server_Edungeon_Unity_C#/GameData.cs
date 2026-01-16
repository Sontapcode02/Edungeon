using System;
using System.Collections.Generic;

// --- GÓI TIN CƠ BẢN ---
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
    public string questionsJson; // Chứa bộ câu hỏi từ CSV của Host
}

// --- DỮ LIỆU CÂU HỎI ---
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

// --- TRẠNG THÁI NGƯỜI CHƠI (Để di chuyển & Đồng bộ) ---
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

// --- TIẾN TRÌNH TRONG GAME (Để hiện Leaderboard) ---
[System.Serializable]
public class PlayerProgress
{
    public string playerId;
    public string playerName;
    public float progressPercentage; // [THÊM] Để hiện % trên thanh Leaderboard
    public int score;
    public bool isAlive = true;      // [THÊM] Để đổi màu dòng nếu người chơi thua
}