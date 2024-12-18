using System;

namespace PersistenceServer.RPCs
{
    public class LoginServer : BaseRpc
    {
        public LoginServer()
        {
            RpcType = RpcType.RpcLoginServer; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string pw = reader.ReadMmoString();
            int port = reader.ReadInt32();
            string level = reader.ReadMmoString();
            if (Server!.Settings.ServerPassword == pw)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Server logged in. Map: {level}, port: {port}");
                Server!.Processor.ConQ.Enqueue(async () => await ProcessLoginServer(port, level, connection));
            }
            else
            {
                Console.WriteLine($"Refusing server login: wrong password. Server tried: {pw}");
                _ = connection.Disconnect(); // not awaited
            }
        }

        private async Task ProcessLoginServer(int port, string level, UserConnection conn)
        {
            Server!.GameLogic.ServerConnected(conn, new GameServer(port, level));

            // retreive all persistent objects
            var persistentObjects = await Server!.Database.GetPersistentObjects(port, level);

            // retreive the server's serialized data and send it back to server
            string? serverInfo = await Server!.Database.GetServerInfo(port, level);

            // send the login message
            byte[] loginMsg = MergeByteArrays(ToBytes(RpcType.RpcLoginServer), WriteMmoString(serverInfo ?? ""), ToBytes(persistentObjects.Count)) ;
            conn.Send(loginMsg);            

            // send all persistent objects in DB to the server identified by its port & level            
            foreach (var obj in persistentObjects)
            {
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcSavePersistentObject), ToBytes(obj.ObjectId), WriteMmoString(obj.JsonString));
                conn.Send(msg);
            }
        }
    }
}