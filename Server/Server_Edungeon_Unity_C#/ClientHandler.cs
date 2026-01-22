using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer
{
    public class ClientHandler : IClientConnection
    {
        // TCP Fields
        private TcpClient _client;
        private NetworkStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;

        // WebSocket Fields
        private WebSocket _webSocket;

        private PlayerSession _session;
        public DateTime StartTime { get; private set; }

        private bool IsWebSocket => _webSocket != null;

        // Constructor TCP
        public ClientHandler(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);
            _session = new PlayerSession(string.Empty, string.Empty, this);
        }

        // Constructor WebSocket
        public ClientHandler(WebSocket ws)
        {
            _webSocket = ws;
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
                if (!string.IsNullOrEmpty(_session.PlayerId))
                {
                    Console.WriteLine($"[Client-TCP] {_session.PlayerId} disconnected.");
                }
                Cleanup();
            }
        }

        public async Task RunWebSocket()
        {
            byte[] buffer = new byte[1024 * 32]; // 32KB buffer
            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    // WebSocket can read messages in chunks (frames)
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                                break;
                            }
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        ms.Seek(0, SeekOrigin.Begin);
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            string json = await reader.ReadToEndAsync();
                            ProcessPacket(json);
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"[Client-WS] {_session.PlayerId} disconnected.");
            }
            finally
            {
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
                        Send(new Packet { type = "ERROR", payload = "Room ID already exists!" });
                        return;
                    }

                    Room newRoom = new Room(newRoomId, _session.PlayerId);

                    // --- [NEW] RECEIVE QUESTIONS FROM CLIENT ---
                    if (!string.IsNullOrEmpty(createData.questionsJson))
                    {
                        try
                        {
                            var clientQuestions = JsonConvert.DeserializeObject<List<QuestionData>>(createData.questionsJson);
                            newRoom.Questions = clientQuestions;
                            Console.WriteLine($"✅ [ROOM {newRoomId}] Received {clientQuestions.Count} questions from Host.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ [ERROR] Error reading questions from Host: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ [ROOM {newRoomId}] Host sent no questions.");
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
                        Send(new Packet { type = "ERROR", payload = "Room not found!" });
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
            if (_session.CurrentRoom == null) return;

            // Security check: Only Host can perform actions
            if (_session.PlayerId != _session.CurrentRoom.HostId) return;

            if (actionName == "START_GAME")
            {
                Console.WriteLine($"Host {_session.PlayerId} started game. Broadcasting OPEN_GATE...");
                _session.CurrentRoom.StartTime = DateTime.Now;
                _session.CurrentRoom.IsGameStarted = true;
                _session.CurrentRoom.Broadcast(new Packet { type = "OPEN_GATE", payload = "" });
            }
            else if (actionName == "PAUSE_GAME" || actionName == "RESUME_GAME")
            {
                // Delegate to Room's HandlePacket which already has logic for these types
                _session.CurrentRoom.HandlePacket(_session, new Packet { type = actionName, payload = "" });
            }
        }

        public void Send(Packet packet)
        {
            try
            {
                string json = JsonConvert.SerializeObject(packet);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                if (IsWebSocket)
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        // Fire and forget send
                        _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                else
                {
                    lock (_writer)
                    {
                        _writer.Write(buffer.Length);
                        _writer.Write(buffer);
                        _writer.Flush();
                    }
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

            if (_client != null) _client.Close();
            if (_webSocket != null) _webSocket.Dispose();
        }
    }
}