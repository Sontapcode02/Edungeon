using System;

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

    // If IClientConnection is missing, define a minimal interface as a placeholder
    // Remove this if you already have the interface elsewhere in your project
    public interface IClientConnection
    {
        void Send(Packet packet);
    }
}