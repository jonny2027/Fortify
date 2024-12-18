namespace PersistenceServer.RPCs
{
    internal class KeepAliveProbe : BaseRpc
    {
        public KeepAliveProbe()
        {
            RpcType = RpcType.RpcKeepAliveProbe; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(connection));
        }

        private void ProcessMessage(UserConnection senderConn)
        {
            byte[] msgAcknowledge = MergeByteArrays(ToBytes(RpcType.RpcKeepAliveProbe));
            senderConn.Send(msgAcknowledge);
        }
    }
}