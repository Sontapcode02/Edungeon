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
                    // Console.WriteLine("[Server-TCP] New client connected."); // Silenced to reduce health-check spam

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
    }
}