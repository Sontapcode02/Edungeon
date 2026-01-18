using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    [Header("Configuration")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 7777;
    // URL for WebSocket (used when building for WebGL)
    public string wsServerUrl = "ws://127.0.0.1:7780";

#if !UNITY_WEBGL || UNITY_EDITOR
    private TcpClient _client;
    private NetworkStream _stream;
    private BinaryReader _reader;
    private BinaryWriter _writer;
    private Thread _receiveThread;
#endif

    private bool _isConnected;
    private ConcurrentQueue<Packet> _packetQueue = new ConcurrentQueue<Packet>();
    public Action<Packet> OnPacketReceived;
    public string MyPlayerId { get; set; }
    public Action<string> OnCheckRoomResult;
    public System.Action<string> OnCreateRoomResult;

    // --- DLL IMPORT FOR WEBGL ---
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebSocketConnect(string url);

    [DllImport("__Internal")]
    private static extern void WebSocketSend(byte[] data, int length);

    [DllImport("__Internal")]
    private static extern void WebSocketClose();

    [DllImport("__Internal")]
    private static extern int WebSocketState();
#endif

    void Awake()
    {
        // [HOTFIX] Force use port 7780 to avoid Inspector caching old values
        wsServerUrl = "ws://127.0.0.1:7780";

        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public void ConnectAndJoin(string playerName, string roomId, bool isHost)
    {
        MyPlayerId = null;

#if UNITY_WEBGL && !UNITY_EDITOR
        // --- WEBGL CONNECT ---
        if (!_isConnected)
        {
             // Debug.Log($"[WebGL] Connecting to WS URL: {wsServerUrl}");
             WebSocketConnect(wsServerUrl);
             // WebGL connects asynchronously, we wait for OnWebSocketOpen callback
             // Logic sends handshake immediately, so we use Coroutine to wait
             StartCoroutine(WaitForConnectionAndSendHandshake(playerName, roomId, isHost));
             return;
        }
#else
        // --- TCP CONNECT ---
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
                Debug.Log("Connected to Server!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Fatal connection error: {e.Message}");
                return;
            }
        }
#endif

        SendHandshake(playerName, roomId, isHost);
    }

    private void SendHandshake(string playerName, string roomId, bool isHost)
    {
        // Debug.Log($"Sending handshake... Role: {(isHost ? "HOST" : "CLIENT")} | Room: {roomId}");

        var handshake = new HandshakeData { playerName = playerName, roomId = roomId };
        string payloadJson = JsonConvert.SerializeObject(handshake);

        Send(new Packet
        {
            type = isHost ? "CREATE_ROOM" : "JOIN_ROOM",
            playerId = null,
            payload = payloadJson
        });
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private System.Collections.IEnumerator WaitForConnectionAndSendHandshake(string playerName, string roomId, bool isHost)
    {
        // Debug.Log("Waiting for WebSocket connection...");
        // Wait max 5 seconds
        float timeout = 5f;
        while (WebSocketState() != 1 && timeout > 0) // 1 = OPEN
        {
            yield return null;
            timeout -= Time.deltaTime;
        }

        if (WebSocketState() == 1)
        {
            _isConnected = true;
            Debug.Log("WebSocket connected! Sending handshake.");
            SendHandshake(playerName, roomId, isHost);
        }
        else
        {
            Debug.LogError("WebSocket connection failed (Timeout).");
        }
    }
#endif

    public void ConnectOnly()
    {
        if (_isConnected) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Debug.Log($"[WebGL] ConnectOnly - URL: {wsServerUrl}");
        WebSocketConnect(wsServerUrl);
        // For WebGL, isConnected will be set to true in OnWebSocketOpen callback
#else
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
            Debug.Log("TCP Connected to Server");
        }
        catch (Exception e)
        {
            Debug.LogError("Connection error: " + e.Message);
        }
#endif
    }

    public void SendCheckRoom(string roomId)
    {
        if (!_isConnected) ConnectOnly();

        // For WebGL, ensure connection before sending
#if UNITY_WEBGL && !UNITY_EDITOR
         StartCoroutine(WaitConnectAndSendCheckRoom(roomId));
#else
        Send(new Packet
        {
            type = "CHECK_ROOM",
            payload = roomId
        });
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private System.Collections.IEnumerator WaitConnectAndSendCheckRoom(string roomId)
    {
         float timeout = 5f;
         while (WebSocketState() != 1 && timeout > 0)
         {
             yield return null;
             timeout -= Time.deltaTime;
         }
         
         if (WebSocketState() == 1)
         {
             _isConnected = true;
            Send(new Packet
            {
                type = "CHECK_ROOM",
                payload = roomId
            });
         }
    }
#endif

    public void SendJoinRoom(string playerName, string roomId)
    {
        if (!_isConnected)
        {
            Debug.LogError("[SocketClient] SendJoinRoom failed: No connection!");
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

        // Debug.Log($"[SocketClient] Sent JOIN_ROOM: {playerName} to room {roomId}");
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private void ReceiveLoop()
    {
        Debug.Log(">>> [ReceiveLoop] Listening to Server...");

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
                        Debug.LogWarning(">>> [ReceiveLoop] Read header failed. Disconnecting.");
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

                    // 3. Deserialize
                    Packet packet = JsonConvert.DeserializeObject<Packet>(json);

                    if (packet != null)
                    {
                        // Debug.Log($">>> [ReceiveLoop] Decoded! Type: {packet.type} | Queue: Enqueued.");
                        _packetQueue.Enqueue(packet);
                    }
                    else
                    {
                        Debug.LogError(">>> [ReceiveLoop] Decode failed: Packet is null!");
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($">>> [ReceiveLoop] FATAL ERROR: {e.Message}\n{e.StackTrace}");
                _isConnected = false;
                break;
            }
        }
        Debug.Log(">>> [ReceiveLoop] Stopped listening.");
    }
#endif

    // --- WEBGL CALLBACKS (Called from JS) ---
    public void OnWebSocketOpen()
    {
        Debug.Log("WebSocket Connected (JS Callback)");
        _isConnected = true;
    }

    public void OnWebSocketMessage(string jsonArgs)
    {
        // Args is JSON {ptr, length}
        var args = JsonConvert.DeserializeObject<WebSocketMessageArgs>(jsonArgs);

        byte[] buffer = new byte[args.length];
        Marshal.Copy(new IntPtr(args.ptr), buffer, 0, args.length);

        // Free memory on JS side if needed (depending on implementation, usually JS GC handles it or uses free() in JS)
        // Here simplified convert buffer to string

        string json = Encoding.UTF8.GetString(buffer);
        Packet packet = JsonConvert.DeserializeObject<Packet>(json);
        if (packet != null)
        {
            _packetQueue.Enqueue(packet);
        }

        // Note: Needs free memory if using _malloc in JS. 
        // But in sample JS code we haven't added free function, this is simple demo.
    }

    public void OnWebSocketClose(int code)
    {
        Debug.Log($"WebSocket Loop Close: {code}");
        _isConnected = false;
    }

    public void OnWebSocketError(string error)
    {
        Debug.LogError($"WebSocket Error: {error}");
        _isConnected = false;
    }

    public void OnWebSocketMessageText(string json)
    {
        // Receive Text message directly from JS
        Packet packet = JsonConvert.DeserializeObject<Packet>(json);
        if (packet != null)
        {
            _packetQueue.Enqueue(packet);
        }
    }

    private class WebSocketMessageArgs { public int ptr; public int length; }

    void Update()
    {
        int processLimit = 20;
        int processedCount = 0;
        while (_packetQueue.Count > 0 && processedCount < processLimit)
        {
            if (_packetQueue.TryDequeue(out Packet packet))
            {
                if (packet.type == "CHECK_ROOM_RESPONSE")
                {
                    OnCheckRoomResult?.Invoke(packet.payload);
                }
                OnPacketReceived?.Invoke(packet);
                if (packet.type == "ROOM_CREATED")
                {
                    MyPlayerId = packet.playerId;
                    Debug.Log($"✅ [HOST] Identity received from Server: {MyPlayerId}");
                    OnCreateRoomResult?.Invoke("SUCCESS");
                }
                else if (packet.type == "ERROR")
                {
                    OnCreateRoomResult?.Invoke(packet.payload);
                }
            }
            processedCount++;
        }
    }

    public void Send(Packet packet)
    {
        if (!_isConnected)
        {
            Debug.LogWarning("[SocketClient] Send failed: Not connected!");
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

#if UNITY_WEBGL && !UNITY_EDITOR
            WebSocketSend(buffer, buffer.Length);
#else
            if (_writer != null)
            {
                lock (_writer)
                {
                    _writer.Write(buffer.Length);
                    _writer.Write(buffer);
                    _writer.Flush();
                }
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketClient] Error sending message: {e.Message}");
            _isConnected = false;
        }
    }

    public void Disconnect()
    {
        if (!_isConnected) return;

        Debug.Log("[SocketClient] Disconnecting...");
        _isConnected = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        WebSocketClose();
#else
        try
        {
            if (_stream != null) _stream.Close();
            if (_client != null) _client.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error closing connection: {e.Message}");
        }
        finally
        {
            _stream = null;
            _client = null;
            _reader = null;
            _writer = null;
        }
#endif
        MyPlayerId = null;
        Packet temp;
        while (_packetQueue.TryDequeue(out temp)) { }
        Debug.Log("[SocketClient] Disconnected successfully.");
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    // Add new methods to SocketClient:
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
        var data = new { questionId = questionId, answerIndex = answerIndex };
        string json = JsonConvert.SerializeObject(data);

        // Debug.Log($">>> [NET] Sending answer: Index {answerIndex} for question ID {questionId}");

        Send(new Packet
        {
            type = "ANSWER",
            payload = json
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