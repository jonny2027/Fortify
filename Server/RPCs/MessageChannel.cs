namespace PersistenceServer.RPCs
{
    public class MessageChannel : BaseRpc
    {
        public MessageChannel()
        {
            RpcType = RpcType.RpcMessageChannel; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            int channel = reader.ReadInt32();
            string message = reader.ReadMmoString();
            bool talkAsGM = reader.ReadBoolean();
            int maxLength = 255;
            // Trim message by maxLength (255 characters)
            message = message.Length <= maxLength ? message : message[..maxLength]; // .. is a C# 8.0 Range Operator https://www.codeguru.com/csharp/c-8-0-ranges-and-indices-types/
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(channel, message, talkAsGM, connection));
        }

        private void ProcessMessage(int channel, string message, bool talkAsGM, UserConnection connection)
        {
            var sender = Server!.GameLogic.GetPlayerByConnection(connection);
            if (sender == null) return; // player could've disconnected in the meantime, so we abort
            var charName = sender.Name;

            // if talkAsGM is true, but sender is not a GM, simply reset it to false
            if (talkAsGM && !sender.IsGm())
            {
                talkAsGM = false;
            }

            // Say is channel 0
            // "Say" must only be displayed in vicinity of the person who says it, so we'll tell the game server
            // to multicast it on character, whose netcull distance will be used as vicinity range
            if (channel == 0)
            {
                string GM = talkAsGM ? "<GM>" : "";
                Console.WriteLine($"{DateTime.Now:HH:mm} [Say] {GM}{charName}: \"{message}\"");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcMessageChannel), WriteMmoString(charName), WriteMmoString(message), ToBytes(talkAsGM));
                //@TODO: optimize by sending only to the right server
                foreach(var serverConn in Server!.GameLogic.GetAllServerConnections())
                {
                    serverConn.Send(msg);
                }
            }

            // Global is channel 1
            if (channel == 1)
            {
                string GM = talkAsGM ? "<GM>" : "";
                Console.WriteLine($"{DateTime.Now:HH:mm} [Global] {GM}{charName}: \"{message}\"");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcMessageChannel), ToBytes(channel), WriteMmoString(charName), WriteMmoString(message), ToBytes(talkAsGM));
                var players = Server!.GameLogic.GetAllPlayerConnections();
                foreach(var player in players)
                {
                    player.Send(msg);
                }
            }
        }
    }
}