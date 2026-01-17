using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic; // Thêm cái này để dùng List
using Newtonsoft.Json;

namespace GameServer
{
    public class ClientHandler : IClientConnection
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;
        private PlayerSession _session;
        public DateTime StartTime { get; private set; }
        public ClientHandler(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);

            _session = new PlayerSession(string.Empty, string.Empty, this);
        }

        public void Run()
        {
            try
            {
                while (_client.Connected)
                {
                    int length = _reader.ReadInt32();
                    byte[] buffer = _reader.ReadBytes(length);
                    string json = Encoding.UTF8.GetString(buffer);
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
            Packet packet = JsonConvert.DeserializeObject<Packet>(json);
            if (packet == null) return;

            if (string.IsNullOrEmpty(_session.PlayerId)) _session.PlayerId = packet.playerId;

            switch (packet.type)
            {
                case "CREATE_ROOM":
                    string hostId = "Host_" + Guid.NewGuid().ToString().Substring(0, 6);
                    _session.PlayerId = hostId;

                    var createData = JsonConvert.DeserializeObject<HandshakeData>(packet.payload);
                    string newRoomId = createData.roomId;
                    _session.PlayerName = createData.playerName;

                    if (Server.Rooms.ContainsKey(newRoomId))
                    {
                        Send(new Packet { type = "ERROR", payload = "ID phòng đã tồn tại!" });
                        return;
                    }

                    Room newRoom = new Room(newRoomId, _session.PlayerId);

                    // --- [MỚI] NHẬN BỘ CÂU HỎI TỪ CLIENT GỬI LÊN ---
                    if (!string.IsNullOrEmpty(createData.questionsJson))
                    {
                        try
                        {
                            // Giải mã JSON thành List câu hỏi
                            var clientQuestions = JsonConvert.DeserializeObject<List<QuestionData>>(createData.questionsJson);

                            // Gán vào phòng (Giả sử class Room có biến Questions public)
                            newRoom.Questions = clientQuestions;

                            Console.WriteLine($"✅ [ROOM {newRoomId}] Đã nhận {clientQuestions.Count} câu hỏi từ Host.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ [ERROR] Lỗi đọc câu hỏi từ Host: {ex.Message}");
                            // Nếu lỗi thì có thể thêm vài câu mặc định ở đây để chống crash
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ [ROOM {newRoomId}] Host không gửi câu hỏi nào.");
                    }
                    // ------------------------------------------------

                    Server.Rooms.TryAdd(newRoomId, newRoom);
                    newRoom.Join(_session);

                    Send(new Packet { type = "ROOM_CREATED", payload = "Success", playerId = hostId });
                    break;

                case "JOIN_ROOM":
                    var joinData = JsonConvert.DeserializeObject<HandshakeData>(packet.payload);
                    string roomIdToJoin = joinData.roomId;
                    _session.PlayerName = joinData.playerName;

                    if (Server.Rooms.TryGetValue(roomIdToJoin, out Room room))
                    {
                        string clientId = "Guest_" + Guid.NewGuid().ToString().Substring(0, 6);
                        _session.PlayerId = clientId;
                        room.Join(_session);
                        Send(new Packet { type = "JOIN_SUCCESS", payload = "Success", playerId = clientId });
                    }
                    else
                    {
                        Send(new Packet { type = "ERROR", payload = "Không tìm thấy phòng!" });
                    }
                    break;

                case "CHAT_MESSAGE":
                    if (_session.CurrentRoom != null) _session.CurrentRoom.HandleChat(_session, packet.payload);
                    break;

                case "HOST_ACTION":
                    HandleHostAction(packet.payload);
                    if (_session.CurrentRoom != null && _session.PlayerId == _session.CurrentRoom.HostId)
                    {
                        if (packet.payload == "MUTE_CHAT") _session.CurrentRoom.ToggleChat(true);
                        else if (packet.payload == "UNMUTE_CHAT") _session.CurrentRoom.ToggleChat(false);
                    }
                    break;

                case "CHECK_ROOM":
                    string roomIdToCheck = packet.payload;
                    bool exists = Server.Rooms.ContainsKey(roomIdToCheck);
                    Send(new Packet { type = "CHECK_ROOM_RESPONSE", payload = exists ? "FOUND" : "NOT_FOUND" });
                    break;

                default:
                    if (_session.CurrentRoom != null) _session.CurrentRoom.HandlePacket(_session, packet);
                    break;
            }
        }

        void HandleHostAction(string actionName)
        {
            if (actionName == "START_GAME" && _session.CurrentRoom != null)
            {
                Console.WriteLine($"Host {_session.PlayerId} started game. Broadcasting OPEN_GATE...");

                // Gán thời gian bắt đầu vào Room để mọi Player dùng chung mốc này
                _session.CurrentRoom.StartTime = DateTime.Now;
                _session.CurrentRoom.IsGameStarted = true;

                _session.CurrentRoom.Broadcast(new Packet { type = "OPEN_GATE", payload = "" });
            }
        }

        public void Send(Packet packet)
        {
            try
            {
                string json = JsonConvert.SerializeObject(packet);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                lock (_writer)
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
            if (_session.CurrentRoom != null) _session.CurrentRoom.Leave(_session);
            _client.Close();
        }
    }
}