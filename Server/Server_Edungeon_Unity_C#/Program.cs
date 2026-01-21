using System;

namespace GameServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Dungeon Quiz Server";

            // Render providers PORT env var. Default to 7780 if not found.
            string portEnv = Environment.GetEnvironmentVariable("PORT");
            int port = 7780;
            if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int p))
            {
                port = p;
            }

            Console.WriteLine($"[Startup] Server starting on port {port}...");
            Server server = new Server(port);
            server.Start();

            // Keep console open if running locally
            Thread.Sleep(-1);
        }
    }
}