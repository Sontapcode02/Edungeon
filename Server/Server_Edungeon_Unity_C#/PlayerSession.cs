using System;
using System.Collections.Generic;

namespace GameServer
{
    public class PlayerSession
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public int Score { get; set; }
        public float LastX { get; set; }
        public float LastY { get; set; }
        public Room CurrentRoom { get; set; }
        public int CurrentQuestionIndex = 0; // Lưu câu hỏi hiện tại của Player này
        public HashSet<int> CompletedMilestones = new HashSet<int>(); // Lưu ID các con quái đã vượt qua

    
        // Tracking quiz progress
        public int CorrectAnswersCount { get; set; } = 0;
        public int TotalQuestionsAnswered { get; set; } = 0;
        public bool HasReachedFinish { get; set; } = false;
        public DateTime FinishTime { get; set; }

        private     IClientConnection _connection;

        public PlayerSession(string id, string name, IClientConnection connection)
        {
            PlayerId = id;
            PlayerName = name;
            _connection = connection;
        }

        public void Send(Packet packet)
        {
        
            _connection?.Send(packet);
        }

        public void ResetProgress()
        {
            CorrectAnswersCount = 0;
            TotalQuestionsAnswered = 0;
            HasReachedFinish = false;
        }
    }

    public interface IClientConnection
    {
        void Send(Packet packet);
    }
}