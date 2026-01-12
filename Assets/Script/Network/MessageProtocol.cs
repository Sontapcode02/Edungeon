using System;

[Serializable]
public class Packet
{
    public string type;       // CREATE_ROOM, JOIN_ROOM, PLAYER_JOINED, GAME_START, MOVE, ANSWER, ROOM_DESTROYED

    public string playerId;
    public string payload;    // JSON Data
}

[Serializable]
public class HandshakeData
{
    public string playerName;
    public string roomId;
}

[Serializable]
public class PlayerState
{
    public string playerId;
    public string playerName;   
    public float x;
    public float y;
    public int score;
    public bool isReady;
}