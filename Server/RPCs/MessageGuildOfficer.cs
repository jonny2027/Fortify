namespace PersistenceServer.RPCs
{
    internal class MessageGuildOfficer : BaseRpc
    {
        public MessageGuildOfficer()
        {
            RpcType = RpcType.RpcMessageGuildOfficer; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string message = reader.ReadMmoString();
            int maxLength = 255;
            // Trim message by maxLength (255 characters)
            message = message.Length <= maxLength ? message : message[..maxLength]; // .. is a C# 8.0 Range Operator https://www.codeguru.com/csharp/c-8-0-ranges-and-indices-types/
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(message, connection));
        }

        private void ProcessMessage(string message, UserConnection connection)
        {
            var sender = Server!.GameLogic.GetPlayerByConnection(connection);            
            if (sender == null) return;
            var charName = sender.Name;

            var guild = Server!.GameLogic.GetPlayerGuild(connection);
            if (guild == null)
            {
                Console.WriteLine($"Player {charName} attempted a guild message, but is not in a guild");
                return;
            }
            if (sender.GuildRank > Server.Settings.GuildOfficerRank)
            {
                Console.WriteLine($"Player {charName} attempted a guild officer message, but isn't an officer");
                return;
            }

            Console.WriteLine($"{DateTime.Now:HH:mm} [Guild Officer ({guild.Id})] {charName}: \"{message}\"");
            // Channel 7 is guild officer channel, see EChatMsgChannel in ue5
            byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcMessageChannel), ToBytes(7), WriteMmoString(charName), WriteMmoString(message), ToBytes(false) /*not a GM message*/);
            var players = guild.GetOnlineOfficers(Server.Settings.GuildOfficerRank);
            foreach (var player in players)
            {
                player.Conn.Send(msg);
            }
        }
    }
}