using Microsoft.AspNetCore.Hosting.Server;
using System.Net;

namespace PersistenceServer
{
    class Program
    {
        static async Task Main(/*string[] args*/)
        {
            // Reads from settings.ini
            SettingsReader settings = new();
            Database database;
            if (settings.SqlType == SqlType.MySql)
                database = new DatabaseMysql(settings);
            else if (settings.SqlType == SqlType.Sqlite)
                database = new DatabaseSqlite(settings);
            else
                throw new Exception("SQL type not defined in settings.ini");
            await database.CheckCreateDatabase(settings);

            // Create a new TCP-based server. IPAddress.Any = people can connect from any ip.
            var server = new MmoWsServer(settings, database);
            int guildsTotal = await server.RequestGuilds();
            Console.WriteLine($"Guilds received: {guildsTotal}");
            server.Start();
            Console.WriteLine($"Server started on port: {settings.Port}");

            Console.WriteLine("Type 'Q' to exit.");
            Console.WriteLine("Type '!' to restart the server.");            
            Console.WriteLine("Type 'admin <message>' to broadcast a system message to all players.");

            // Perform text input
            for (; ; )
            {
                string? line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.ToLower() == "q")
                    break;

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    await server.Stop();
                    server = new MmoWsServer(settings, database);
                    guildsTotal = await server.RequestGuilds();
                    Console.WriteLine($"Guilds received: {guildsTotal}");
                    server.Start();
                    Console.WriteLine("Done!");
                    continue;
                }

                if (line.StartsWith("admin ")) // Multicast system message to all sessions
                {                    
                    server.BroadcastAdminMessage(line[6..]); // removes "admin " from the message
                    continue;
                }
            }

            // Stop the server
            Console.Write("Server stopping...");
            await server.Stop();
            Console.WriteLine("Done!");
        }
    }
}