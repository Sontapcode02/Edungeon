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
            // 1. TCP Listener (Legacy / Editor)
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[Server] TCP Started on port {_port}. Waiting for clients...");

            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();

            // 2. WebSocket Listener (WebGL)
            try
            {
                _httpListener = new HttpListener();
                // Listen on port + 3 (e.g. 7780) because 7778 is busy
                _httpListener.Prefixes.Add($"http://*:{_port + 3}/");
                _httpListener.Start();
                Console.WriteLine($"[Server] WebSocket Started on port {_port + 3}. Waiting for WebGL clients...");

                Task.Run(AcceptWebSockets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Upload failed to start WebSocket Listener: {ex.Message}");
                Console.WriteLine("Make sure to run as Administrator or add urlacl reservation.");
            }
        }

        private void AcceptClients()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Console.WriteLine("[Server-TCP] New client connected.");

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