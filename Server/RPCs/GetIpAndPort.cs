namespace PersistenceServer.RPCs
{
    public class GetIpAndPort : BaseRpc
    {
        public GetIpAndPort()
        {
            RpcType = RpcType.RpcGetIpAndPort; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            var charId = reader.ReadInt32();
            // @TODO: depending on charid, we might want the player to connect to different IP or PORT, especially PORT if it represents different areas
            // for now we ignore it, but we reserve the possibility of using it later
            
            Server!.Processor.ConQ.Enqueue(() => {
                Console.WriteLine($"Returning game server IP and port for char id: {charId}");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcGetIpAndPort), WriteMmoString($"{Server!.Settings.GameServerIp}:7779"));
                connection.Send(msg);
            });
        }
    }
}