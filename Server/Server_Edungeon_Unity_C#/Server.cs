using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer
{
    public class Server
    {
        private int _port;
        private TcpListener _listener;
        private HttpListener _httpListener;
        private bool _isRunning;

        // Thread-safe dictionary for Rooms
        public static ConcurrentDictionary<string, Room> Rooms = new ConcurrentDictionary<string, Room>();

        // [SECURITY] Connection Throttling
        private static ConcurrentDictionary<string, int> _ipConnectionCounts = new ConcurrentDictionary<string, int>();
        private const int MAX_CONNECTIONS_PER_IP = 20; // Increased to 20 to allow small groups (NAT)

        public Server(int port)
        {
            _port = port;
        }

        public void Start()
        {
            // 1. WebSocket Listener (Primary for WebGL/Render)
            try
            {
                _httpListener = new HttpListener();
                // Listen on PROXY Port (e.g. 7780)
                _httpListener.Prefixes.Add($"http://*:{_port}/");
                _httpListener.Start();
                Console.WriteLine($"[Server] WebSocket Started on port {_port}. Waiting for WebGL clients...");
                Console.WriteLine($"[VERSION] 1.0.1 - FIXED ROOM FULL CHECK (Wait for this log on Render)");

                Task.Run(AcceptWebSockets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Failed to start WebSocket Listener: {ex.Message}");
            }

            // 2. TCP Listener (Legacy / Editor / Secondary)
            try
            {
                int tcpPort = _port + 1;
                _listener = new TcpListener(IPAddress.Any, tcpPort);
                _listener.Start();
                _isRunning = true;
                Console.WriteLine($"[Server] TCP Started on port {tcpPort}. Waiting for Editor clients...");

                Thread acceptThread = new Thread(AcceptClients);
                acceptThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] TCP Listener failed: {ex.Message}");
            }
        }

        private void AcceptClients()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    // [SECURITY] Check Connection Limit
                    string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    if (!TryAddConnection(clientIp))
                    {
                        Console.WriteLine($"[Security] Blocked connection from {clientIp} (Limit reached)");
                        client.Close();
                        continue;
                    }

                    client.NoDelay = true; // [OPTIMIZE] Disable Nagle's Algorithm for lower latency
                    client.NoDelay = true; // [OPTIMIZE] Disable Nagle's Algorithm for lower latency
                    // Console.WriteLine("[Server-TCP] New client connected."); // [DEBUG] Silenced to prevent Health Check spam

                    ClientHandler handler = new ClientHandler(client);
                    Thread clientThread = new Thread(handler.Run);
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server-TCP] Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task AcceptWebSockets()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);

                        // [SECURITY] Check Connection Limit
                        string clientIp = context.Request.RemoteEndPoint.Address.ToString();
                        if (!TryAddConnection(clientIp))
                        {
                            Console.WriteLine($"[Security] Blocked WS connection from {clientIp} (Limit reached)");
                            wsContext.WebSocket.Abort();
                            continue;
                        }

                        Console.WriteLine("[Server-WS] New WebGL client connected.");

                        ClientHandler handler = new ClientHandler(wsContext.WebSocket);
                        // WebSocket handler runs async
                        _ = Task.Run(() => handler.RunWebSocket());
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server-WS] Error accepting client: {ex.Message}");
                }
            }
        }


        // [SECURITY] Connection Throttling Methods
        private bool TryAddConnection(string ip)
        {
            _ipConnectionCounts.AddOrUpdate(ip, 1, (key, oldValue) => oldValue + 1);
            if (_ipConnectionCounts[ip] > MAX_CONNECTIONS_PER_IP)
            {
                // Rollback if exceeded
                RemoveConnection(ip);
                return false;
            }
            return true;
        }

        public static void RemoveConnection(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return;

            _ipConnectionCounts.AddOrUpdate(ip, 0, (key, oldValue) => Math.Max(0, oldValue - 1));
            // Console.WriteLine($"[Security] Connection removed for {ip}. Current count: {_ipConnectionCounts[ip]}");
        }
    }
}