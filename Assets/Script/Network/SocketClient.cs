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
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    // XÓA HÀM START() ĐI ĐỂ KHÔNG TỰ KẾT NỐI
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
                return; // Không kết nối được thì nghỉ chơi
            }
        }

        

        Debug.Log($"Đang gửi lệnh... Role: {(isHost ? "HOST" : "CLIENT")} | Room: {roomId}");

        // 3. CHUẨN BỊ DỮ LIỆU
        var handshake = new HandshakeData { name = playerName, roomId = roomId };
        string payloadJson = JsonConvert.SerializeObject(handshake);

        // 4. GỬI GÓI TIN XÁC NHẬN
        // Nếu là Host -> Gửi lệnh TẠO (CREATE_ROOM)
        // Nếu là Khách -> Gửi lệnh VÀO (JOIN_ROOM)
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
        if (!_isConnected) ConnectOnly(); // Chưa kết nối thì kết nối luôn

        Send(new Packet
        {
            type = "CHECK_ROOM",
            payload = roomId // Gửi mỗi cái ID phòng lên thôi
        });
    }
    private void ReceiveLoop()
    {
        Debug.Log(">>> [ReceiveLoop] Bắt đầu lắng nghe Server...");

        while (_isConnected)
        {
            try
            {
                // Kiểm tra xem có dữ liệu đến không
                if (_client.Available > 0 || _stream.DataAvailable)
                {
                    // 1. Đọc độ dài (4 byte đầu tiên)
                    // Nếu Server gửi thiếu header này là Client treo luôn ở đây
                    byte[] lengthBuffer = new byte[4];
                    int bytesRead = _stream.Read(lengthBuffer, 0, 4);

                    if (bytesRead < 4)
                    {
                        Debug.LogWarning(">>> [ReceiveLoop] Đọc header thất bại (không đủ 4 byte). Ngắt kết nối.");
                        _isConnected = false;
                        break;
                    }

                    int length = BitConverter.ToInt32(lengthBuffer, 0); // Convert byte sang int
                    // Debug.Log($">>> [ReceiveLoop] Đã nhận tín hiệu! Độ dài gói tin: {length}");

                    if (length <= 0) continue; // Bỏ qua nếu gói tin rỗng

                    // 2. Đọc nội dung (Payload)
                    byte[] buffer = new byte[length];
                    int totalBytesRead = 0;

                    // Vòng lặp đọc cho đến khi đủ dữ liệu (đề phòng mạng lag bị cắt gói)
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
                OnCheckRoomResult?.Invoke(packet.payload); // Bắn tin "FOUND" hoặc "NOT_FOUND" ra UI
            }
            OnPacketReceived?.Invoke(packet);
        }
    }

    public void Send(Packet packet)
    {
        // 1. Kiểm tra kết nối trước
        if (!_isConnected)
        {
            Debug.LogWarning("[SocketClient] Send thất bại: Chưa kết nối Server!");
            return;
        }

        // 2. Gắn ID của mình vào nếu gói tin chưa có
        if (string.IsNullOrEmpty(packet.playerId))
        {
            packet.playerId = MyPlayerId;
        }

        try
        {
            // 3. Đóng gói dữ liệu (Serialize)
            // Dùng Newtonsoft.Json cho chuẩn với Server
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            // 4. Gửi đi (Thread Safe)
            // Dùng lock để đảm bảo không bị tranh chấp nếu gửi từ nhiều luồng
            if (_writer != null)
            {
                lock (_writer)
                {
                    _writer.Write(buffer.Length); // Gửi độ dài trước (4 bytes)
                    _writer.Write(buffer);        // Gửi nội dung tin nhắn sau
                    _writer.Flush();              // QUAN TRỌNG: Đẩy dữ liệu đi ngay lập tức!
                }
            }

            // Log ra để biết là đã gửi thành công
            Debug.Log($"[Client >> Server] Gửi: {packet.type} | Payload: {packet.payload}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketClient] Lỗi khi gửi tin: {e.Message}");
            _isConnected = false; // Ngắt kết nối luôn nếu gửi lỗi để tránh spam
        }
    }

    void OnApplicationQuit()
    {
        _isConnected = false;
        _client?.Close();
    }
}