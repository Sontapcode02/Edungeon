namespace GameServer
{
    public class PlayerSession
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public ClientHandler Handler { get; set; }
        public Room CurrentRoom { get; set; }
        public int Score { get; set; }
        public float LastX { get; set; } = 0;
        public float LastY { get; set; } = 0;

        public void SendPacket(Packet packet)
        {
            Handler.Send(packet);
        }
    }
}