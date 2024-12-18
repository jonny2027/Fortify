namespace PersistenceServer.RPCs
{
    internal class SavePersistentObject : BaseRpc
    {
        public SavePersistentObject()
        {
            RpcType = RpcType.RpcSavePersistentObject; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            int objectId = reader.ReadInt32();
            string jsonString = reader.ReadMmoString();

            Server!.Processor.ConQ.Enqueue(async () => await ProcessSavePersistentObject(objectId, jsonString, connection));
        }

        private async Task ProcessSavePersistentObject(int objectId, string jsonString, UserConnection conn)
        {
            var gameServer = Server!.GameLogic.GetServerByConnection(conn);
            if (gameServer == null)
            {
                Console.WriteLine("Illegal action: not a server tried SavePersistentObject RPC. This should never happen.");
                return;
            }
            // if it's an empty json, delete the object
            if (jsonString == "{}")
            {
                await Server!.Database.DeletePersistentObject(gameServer.Level, gameServer.Port, objectId);
            }
            else
            {
                await Server!.Database.SavePersistentObject(gameServer.Level, gameServer.Port, objectId, jsonString);
            }
        }
    }
}