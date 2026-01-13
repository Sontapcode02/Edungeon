using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GameServer
{
    public class Server
    {
        private int _port;
        private TcpListener _listener;
        private bool _isRunning;

        // Thread-safe dictionary for Rooms
        public static ConcurrentDictionary<string, Room> Rooms = new ConcurrentDictionary<string, Room>();

        public Server(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[Server] Started on port {_port}. Waiting for clients...");

            // Start accepting clients
            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();
        }

        private void AcceptClients()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Console.WriteLine("[Server] New client connected.");

                    // Spawn a handler for this client
                    ClientHandler handler = new ClientHandler(client);
                    Thread clientThread = new Thread(handler.Run);
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Error accepting client: {ex.Message}");
                }
            }
        }
    }
}