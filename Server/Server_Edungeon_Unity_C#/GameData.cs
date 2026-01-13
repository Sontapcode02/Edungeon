using System;
using System.Collections.Generic;

// --- FILE DÙNG CHUNG (Cập nhật chuẩn) ---

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
    public string quizFilePath;
}

[System.Serializable]
public class Question
{
    public int Id;
    public string QuestionText;
    public string[] Answers;
    public int CorrectIndex;
    public int TimeLimit;
}

// SỬA LẠI CLASS NÀY:
[System.Serializable]
public class PlayerState
{
    public string playerId;   // Sửa 'id' thành 'playerId'
    public string playerName; // Thêm biến này vào
    public float x;
    public float y;
    public int score;
    public bool isReady;      // Thêm luôn cho đủ bộ (thường game hay dùng)
}