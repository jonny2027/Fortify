namespace PersistenceServer.RPCs
{
    public class Connected : BaseRpc
    {
        public Connected()
        {
            RpcType = RpcType.RpcConnected; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
#if DEBUG
            Console.Write($"(thread {Environment.CurrentManagedThreadId}) ");
#endif
            Console.WriteLine($"User connected. Session Id: {connection.Id}");
        }
    }
}