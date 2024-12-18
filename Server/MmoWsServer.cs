using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PersistenceServer
{
    public class MmoWsServer
    {        
        public readonly ActionsSyncher Processor;
        public readonly GameLogic GameLogic;
        public readonly Database Database;
        public readonly SettingsReader Settings;
        public delegate void MessageReceivedHandler(RpcType inRpcType, UserConnection conn, BinaryReader reader);
        public event MessageReceivedHandler? OnMessageReceived;
        private readonly IWebHost? host;

        public MmoWsServer(SettingsReader inSettings, Database inDatabase)
        {
            Settings = inSettings;
            // Contains a list of Players, Groups, etc
            GameLogic = new();
            // Launch a Thread that will 'Tick' every 8ms and process Actions on a Concurrent Queue
            Processor = new();
            _ = Processor.Tick();

            Database = inDatabase;

            // Create an instance of each class that inherits from BaseRPC
            List<BaseRpc> rpcReaders = typeof(BaseRpc).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseRpc))).Select(t => (BaseRpc)Activator.CreateInstance(t)!).ToList();
#if DEBUG
            // Print all found readers
            var readerNames = rpcReaders.Select(rpcReader => rpcReader.GetType().ToString().Replace("PersistenceServer.", "")).ToArray();
            Console.WriteLine($"Found RPC readers: {rpcReaders.Count} [{string.Join(", ", readerNames)}]");
#endif
            // Check that their rpc type is set - the check only happens in Debug            
            foreach (var reader in rpcReaders) Debug.Assert(reader.RpcType != RpcType.RpcUndef);

            // Subscribe each class to OnMessageReceived event
            foreach (var reader in rpcReaders) reader.SubscribeToMessages(this);

            // Build WS server
            host = new WebHostBuilder()
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Any, inSettings.Port);
            })
            .ConfigureServices(services =>
            {
                services.AddRouting(); // Adds the necessary service for routing
            })
            .Configure(app =>
            {
                app.UseWebSockets();
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.Map("/mmo", async context =>
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            var userConnection = new UserConnection(this, webSocket);
                            await userConnection.HandleConnectionAsync();
                        }
                        else
                        {
                            Console.WriteLine("Rejecting connection with code 400");
                            //foreach (var header in context.Request.Headers)
                            //{
                            //    Console.WriteLine($"{header.Key}: {header.Value}");
                            //}
                            context.Response.StatusCode = 400;
                        }
                    });
                });

                app.Use(async (context, next) =>
                {
                    // in case there's other middleware some day, it'll get the chance to process this request
                    await next();
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders(); // Removes all log providers, including the console one.
                logging.AddFilter("Microsoft", LogLevel.Warning) // Log only warnings or errors from Microsoft libraries
                       .AddFilter("System", LogLevel.Warning); // Log only warnings or errors from System libraries
            })
            .Build();
        }

        public void Start()
        {
            host?.Start();
        }

        public async Task Stop()
        {
            if (host != null)
                await host.StopAsync();
        }

        public void InvokeOnMessageReceived(RpcType inRpcType, UserConnection conn, BinaryReader reader)
        {
            /// This will call ReadRpc() on all BaseRPC subclasses that are of the correct RpcType, <see cref="BaseRPC"/>
            OnMessageReceived?.Invoke(inRpcType, conn, reader);
        }

        public void BroadcastAdminMessage(string msg)
        {
            byte[] msgBytes = BaseRpc.WriteMmoString(msg);
            BinaryReader reader = new(new MemoryStream(msgBytes));
            // since we're sending it from the console and not from an actual ue5 server, we have to use a little "hack" by finding a random ue5 server connection
            // if we don't find it, it means there are no servers and therefore no players online, and so we skip broadcasting the message
            if (GameLogic.GetAllServerConnections().Length > 0)
                InvokeOnMessageReceived(RpcType.RpcAdminMessage, GameLogic.GetAllServerConnections()[0], reader);
            else
                Console.WriteLine("No connected servers and players to send a message to.");
        }

        public async Task<int> RequestGuilds()
        {
            var guilds = await Database.GetGuilds();
            GameLogic.AssignGuilds(guilds);
            return guilds.Count;
        }
    }
}
