using System;

namespace PersistenceServer.RPCs
{
    internal class SaveServerInfo : BaseRpc
    {
        public SaveServerInfo()
        {
            RpcType = RpcType.RpcSaveServerInfo; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string serialized = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessSaveServerInfo(serialized, connection));
        }

        private async Task ProcessSaveServerInfo(string serializedServerInfo, UserConnection conn)
        {
            var gameServer = Server!.GameLogic.GetServerByConnection(conn);
            if (gameServer == null)
            {
                Console.WriteLine("Illegal action: not a server tried SaveServerInfo RPC. This must never happen: investigate if it does.");
                return;
            }

            await Server!.Database.SaveServerInfo(serializedServerInfo, gameServer.Port, gameServer.Level);
        }
    }
}