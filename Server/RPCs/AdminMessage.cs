using System.Threading.Channels;

namespace PersistenceServer.RPCs
{
    internal class AdminMessage : BaseRpc
    {
        public AdminMessage()
        {
            RpcType = RpcType.RpcAdminMessage; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string message = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(message, connection));
        }

        private void ProcessMessage(string message, UserConnection connection)
        {
            bool isServerMessage = Server!.GameLogic.GetAllServerConnections().Contains(connection);
            if (!isServerMessage) return;

            Console.WriteLine($"{DateTime.Now:HH:mm} [Admin Message]: \"{message}\"");
            byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcAdminMessage), WriteMmoString(message));
            var players = Server!.GameLogic.GetAllPlayerConnections();
            foreach (var player in players)
            {
                player.Send(msg);
            }
        }
    }
}