using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
namespace GameServer
{
    public class ClientHandler
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;
        private PlayerSession _session;

        public ClientHandler(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);
            _session = new PlayerSession { Handler = this };
        }

        public void Run()
        {
            try
            {
                while (_client.Connected)
                {
                    // 1. Read Length (4 bytes int)
                    int length = _reader.ReadInt32();

                    // 2. Read Payload
                    byte[] buffer = _reader.ReadBytes(length);
                    string json = Encoding.UTF8.GetString(buffer);

                    // 3. Process
                    ProcessPacket(json);
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"[Client] {_session.PlayerId} disconnected.");
                Cleanup();
            }
        }

        private void ProcessPacket(string json)
        {
            Packet packet = JsonHelper.FromJson<Packet>(json);

            // Lưu tạm ID kết nối nếu chưa có
            if (string.IsNullOrEmpty(_session.PlayerId)) _session.PlayerId = packet.playerId;
            switch (packet.type)
            {
                case "CREATE_ROOM":
                    string hostId = _session.PlayerName + "Host_" + Guid.NewGuid().ToString().Substring(0, 6);
                    _session.PlayerId = hostId;
                    // Đọc dữ liệu Handshake từ Client
                    var createData = JsonHelper.FromJson<HandshakeData>(packet.payload);
                    string newRoomId = createData.roomId; // Dùng ID mà Client (HomeManager) đã tạo
                    _session.PlayerName = createData.playerName;
                    if (Server.Rooms.ContainsKey(newRoomId))
                    {
                        Send(new Packet { type = "ERROR", payload = "ID phòng đã tồn tại!" });
                        return;
                    }

                    // Tạo phòng mới với HostId là người gửi
                    Room newRoom = new Room(newRoomId, _session.PlayerId);
                    Server.Rooms.TryAdd(newRoomId, newRoom);

                    // Cho Host join vào phòng luôn
                    newRoom.Join(_session);
                    Send(new Packet
                    {
                        type = "ROOM_CREATED",
                        payload = "Success",
                        playerId = hostId // <--- Gửi ID về cho Host biết
                    });
                    break;

                case "JOIN_ROOM":
                    var joinData = JsonHelper.FromJson<HandshakeData>(packet.payload);
                    string roomIdToJoin = joinData.roomId;
                    _session.PlayerName = joinData.playerName;
                    if (Server.Rooms.TryGetValue(roomIdToJoin, out Room room))
                    {
                        string clientId = _session.PlayerName + "_" + Guid.NewGuid().ToString().Substring(0, 6);
                        _session.PlayerId = clientId;
                        room.Join(_session);
                        Send(new Packet
                        {
                            type = "JOIN_SUCCESS",
                            payload = "Success",
                            playerId = clientId // <--- Gửi ID về cho Client biết
                        });
                    }
                    else
                    {
                        Send(new Packet { type = "ERROR", payload = "Không tìm thấy phòng!"  });
                    }
                    break;
                case "CHAT_MESSAGE":
                    if (_session.CurrentRoom != null)
                    {
                        // Gửi nội dung chat vào Room để xử lý
                        _session.CurrentRoom.HandleChat(_session, packet.payload);
                    }
                    break;
                case "HOST_ACTION":

                    HandleHostAction(packet.payload);
                    if (_session.CurrentRoom != null && _session.PlayerId == _session.CurrentRoom.HostId)
                    {
                        if (packet.payload == "MUTE_CHAT")
                        {
                            _session.CurrentRoom.ToggleChat(true);
                        }
                        else if (packet.payload == "UNMUTE_CHAT")
                        {
                            _session.CurrentRoom.ToggleChat(false);
                        }
                    }
                    break;
                case "CHECK_ROOM":
                    // Client gửi lên chỉ để hỏi xem phòng có tồn tại không
                    string roomIdToCheck = packet.payload; // Payload chính là mã phòng

                    bool exists = Server.Rooms.ContainsKey(roomIdToCheck);
                    // Gửi trả lời về ngay
                    Send(new Packet
                    {
                        type = "CHECK_ROOM_RESPONSE",
                        payload = exists ? "FOUND" : "NOT_FOUND"

                    });
                    
                    break;
                default:
                    if (_session.CurrentRoom != null)
                        _session.CurrentRoom.HandlePacket(_session, packet);
                    break;

            }
        }
        void HandleHostAction(string actionName)
        {
            if (actionName == "START_GAME" && _session.CurrentRoom != null)
            {
                Console.WriteLine($"Host {_session.PlayerId} started game. Broadcasting OPEN_GATE...");
                _session.CurrentRoom.Broadcast(new Packet
                {
                    type = "OPEN_GATE",
                    payload = ""
                });
            }
        }
        public void Send(Packet packet)
        {
            try
            {
                string json = JsonHelper.ToJson(packet);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                // Protocol: Length Prefix + Data
                lock (_writer) // Prevent thread collision on write
                {
                    _writer.Write(buffer.Length);
                    _writer.Write(buffer);
                    _writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Send Error] {ex.Message}");
            }
        }

        private void Cleanup()
        {
            if (_session.CurrentRoom != null)
            {
                _session.CurrentRoom.Leave(_session);
            }
            _client.Close();
        }
    }
}