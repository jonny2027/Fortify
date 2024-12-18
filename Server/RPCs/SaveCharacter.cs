namespace PersistenceServer.RPCs
{
    public class SaveCharacter : BaseRpc
    {
        public SaveCharacter()
        {
            RpcType = RpcType.RpcSaveCharacter; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            int charId = reader.ReadInt32();
            string serialized = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessSaveCharacter(charId, serialized, connection));
        }

        private async Task ProcessSaveCharacter(int charId, string serializedCharacter, UserConnection conn)
        {
            if (!Server!.GameLogic.IsServer(conn))
            {
                Console.WriteLine("Illegal action: not a server tried SaveCharacter RPC. This must never happen: investigate if it does.");
                return;
            }

            await Server!.Database.SaveCharacter(charId, serializedCharacter);
        }
    }
}