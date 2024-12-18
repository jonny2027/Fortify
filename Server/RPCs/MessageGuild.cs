using System.Threading.Channels;

namespace PersistenceServer.RPCs
{
    internal class MessageGuild : BaseRpc
    {
        public MessageGuild()
        {
            RpcType = RpcType.RpcMessageGuild; // set it to the RpcType you want to catch
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
            var charName = Server!.GameLogic.GetPlayerName(connection);
            if (charName == "") return;

            var guild = Server!.GameLogic.GetPlayerGuild(connection);
            if (guild == null) {
                Console.WriteLine($"Player {charName} attempted a guild message, but is not in a guild");
                return;
            }

            Console.WriteLine($"{DateTime.Now:HH:mm} [Guild ({guild.Id})] {charName}: \"{message}\"");
            // Channel 5 is guild channel, see EChatMsgChannel in ue5
            byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcMessageChannel), ToBytes(5), WriteMmoString(charName), WriteMmoString(message), ToBytes(false) /*not a GM message*/);
            var players = guild.GetOnlineMembers();
            foreach (var player in players)
            {
                player.Conn.Send(msg);
            }
        }
    }
}