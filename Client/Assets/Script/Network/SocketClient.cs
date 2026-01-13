using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    [Header("Configuration")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 7777;

    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryReader _reader;
    private BinaryWriter _writer;
    private Thread _receiveThread;
    private bool _isConnected;
    private ConcurrentQueue<Packet> _packetQueue = new ConcurrentQueue<Packet>();
    public Action<Packet> OnPacketReceived;
    public string MyPlayerId { get; set; }
    public Action<string> OnCheckRoomResult;
    public System.Action<string> OnCreateRoomResult;
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public void ConnectAndJoin(string playerName, string roomId, bool isHost)
    {
        MyPlayerId = null;
        if (!_isConnected)
        {
            try
            {
                _client = new TcpClient();
                _client.Connect(serverIP, serverPort);
                _stream = _client.GetStream();
                _reader = new BinaryReader(_stream);
                _writer = new BinaryWriter(_stream);
                _isConnected = true;

                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();
                Debug.Log("Đã tạo kết nối mới tới Server!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Lỗi kết nối chết người: {e.Message}");
                return;
            }
        }

        Debug.Log($"Đang gửi lệnh... Role: {(isHost ? "HOST" : "CLIENT")} | Room: {roomId}");

        var handshake = new HandshakeData { playerName = playerName, roomId = roomId };
        string payloadJson = JsonConvert.SerializeObject(handshake);

        Send(new Packet
        {
            type = isHost ? "CREATE_ROOM" : "JOIN_ROOM",
            playerId = null,
            payload = payloadJson
        });
    }
    
    public void ConnectOnly()
    {
        if (_isConnected) return;
        try
        {
            _client = new TcpClient();
            _client.Connect(serverIP, serverPort);
            _stream = _client.GetStream();
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);
            _isConnected = true;

            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
            Debug.Log("Đã kết nối TCP tới Server (Chưa vào phòng)");
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi kết nối: " + e.Message);
        }
    }
    
    public void SendCheckRoom(string roomId)
    {
        if (!_isConnected) ConnectOnly();

        Send(new Packet
        {
            type = "CHECK_ROOM",
            payload = roomId
        });
    }

    // --- THÊM HÀM NÀY ---
    public void SendJoinRoom(string playerName, string roomId)
    {
        if (!_isConnected)
        {
            Debug.LogError("[SocketClient] SendJoinRoom thất bại: Chưa kết nối Server!");
            return;
        }

        var handshakeData = new HandshakeData
        {
            playerName = playerName,
            roomId = roomId
        };

        string payloadJson = JsonConvert.SerializeObject(handshakeData);

        Send(new Packet
        {
            type = "JOIN_ROOM",
            playerId = null,
            payload = payloadJson
        });

        Debug.Log($"[SocketClient] Đã gửi lệnh JOIN_ROOM: {playerName} vào phòng {roomId}");
    }

    private void ReceiveLoop()
    {
        Debug.Log(">>> [ReceiveLoop] Bắt đầu lắng nghe Server...");

        while (_isConnected)
        {
            try
            {
                if (_client.Available > 0 || _stream.DataAvailable)
                {
                    byte[] lengthBuffer = new byte[4];
                    int bytesRead = _stream.Read(lengthBuffer, 0, 4);

                    if (bytesRead < 4)
                    {
                        Debug.LogWarning(">>> [ReceiveLoop] Đọc header thất bại (không đủ 4 byte). Ngắt kết nối.");
                        _isConnected = false;
                        break;
                    }

                    int length = BitConverter.ToInt32(lengthBuffer, 0);

                    if (length <= 0) continue;

                    byte[] buffer = new byte[length];
                    int totalBytesRead = 0;

                    while (totalBytesRead < length)
                    {
                        int read = _stream.Read(buffer, totalBytesRead, length - totalBytesRead);
                        if (read == 0) break;
                        totalBytesRead += read;
                    }

                    string json = Encoding.UTF8.GetString(buffer);
                    Debug.Log($">>> [ReceiveLoop] JSON NHẬN ĐƯỢC: {json}"); // <--- QUAN TRỌNG: Dòng này phải hiện!

                    // 3. Giải mã (Deserialize)
                    Packet packet = JsonConvert.DeserializeObject<Packet>(json);

                    if (packet != null)
                    {
                        Debug.Log($">>> [ReceiveLoop] Đã giải mã thành công! Type: {packet.type} | Queue: Đẩy vào hàng đợi.");
                        _packetQueue.Enqueue(packet);
                    }
                    else
                    {
                        Debug.LogError(">>> [ReceiveLoop] Giải mã thất bại: Packet bị null!");
                    }
                }
                else
                {
                    // Nghỉ 1 tí cho đỡ ngốn CPU nếu chưa có tin
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($">>> [ReceiveLoop] LỖI CHẾT NGƯỜI: {e.Message}\n{e.StackTrace}");
                _isConnected = false;
                break;
            }
        }
        Debug.Log(">>> [ReceiveLoop] Đã dừng lắng nghe.");
    }

    void Update()
    {
        while (_packetQueue.TryDequeue(out Packet packet))
        {
            if (packet.type == "CHECK_ROOM_RESPONSE")
            {
                Console.WriteLine(packet.type);
                OnCheckRoomResult?.Invoke(packet.payload);
            }
            OnPacketReceived?.Invoke(packet);
            if (packet.type == "ROOM_CREATED")
            {
                OnCreateRoomResult?.Invoke("SUCCESS");
            }
            else if (packet.type == "ERROR")
            {
                OnCreateRoomResult?.Invoke(packet.payload);
            }
        }
    }

    public void Send(Packet packet)
    {
        if (!_isConnected)
        {
            Debug.LogWarning("[SocketClient] Send thất bại: Chưa kết nối Server!");
            return;
        }

        if (string.IsNullOrEmpty(packet.playerId))
        {
            packet.playerId = MyPlayerId;
        }

        try
        {
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            if (_writer != null)
            {
                lock (_writer)
                {
                    _writer.Write(buffer.Length);
                    _writer.Write(buffer);
                    _writer.Flush();
                }
            }

            Debug.Log($"[Client >> Server] Gửi: {packet.type} | Payload: {packet.payload}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketClient] Lỗi khi gửi tin: {e.Message}");
            _isConnected = false;
        }
    }
    
    public void Disconnect()
    {
        if (!_isConnected) return;

        Debug.Log("[SocketClient] Đang chủ động ngắt kết nối...");
        _isConnected = false;

        try
        {
            if (_stream != null) _stream.Close();
            if (_client != null) _client.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Lỗi khi đóng kết nối: {e.Message}");
        }
        finally
        {
            _stream = null;
            _client = null;
            _reader = null;
            _writer = null;

            MyPlayerId = null;

            Packet temp;
            while (_packetQueue.TryDequeue(out temp)) { }

            Debug.Log("[SocketClient] Đã ngắt kết nối thành công.");
        }
    }
    
    void OnApplicationQuit()
    {
        _isConnected = false;
        _client?.Close();
    }

    // Thêm các method mới vào SocketClient:
    public void SendProgressUpdate(string playerId, float progress, int checkpoint)
    {
        var payload = new { playerId, progress, checkpoint };
        Send(new Packet
        {
            type = "PROGRESS_UPDATE",
            playerId = playerId,
            payload = JsonConvert.SerializeObject(payload)
        });
    }

    public void SendPlayerDied(string playerId)
    {
        Send(new Packet
        {
            type = "PLAYER_DIED",
            playerId = playerId,
            payload = playerId
        });
    }

    public void SendGameComplete(string playerId)
    {
        Send(new Packet
        {
            type = "GAME_COMPLETE",
            playerId = playerId,
            payload = playerId
        });
    }

    public void SendEnemyEncounter()
    {
        Send(new Packet
        {
            type = "ENEMY_ENCOUNTER",
            payload = ""
        });
    }

    public void SendAnswer(int questionId, int answerIndex)
    {
        var answerData = new { questionId, answerIndex };
        Send(new Packet
        {
            type = "ANSWER",
            payload = JsonConvert.SerializeObject(answerData)
        });
    }

    public void SendReachFinish()
    {
        Send(new Packet
        {
            type = "REACH_FINISH",
            payload = ""
        });
    }
}