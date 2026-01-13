using System;

namespace GameServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Dungeon Quiz TCP Server";
            Server server = new Server(7777); // Listen on port 7777
            server.Start();
        }
    }
}