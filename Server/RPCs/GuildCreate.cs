using System;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace PersistenceServer.RPCs
{
    internal class GuildCreate : BaseRpc
    {
        public GuildCreate()
        {
            RpcType = RpcType.RpcGuildCreate; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string guildName = reader.ReadMmoString();
            int maxLength = 20;
            // Trim message by maxLength (20 characters)
            guildName = guildName.Length <= maxLength ? guildName : guildName[..maxLength]; // .. is a C# 8.0 Range Operator https://www.codeguru.com/csharp/c-8-0-ranges-and-indices-types/
            SanitizeGuildName(ref guildName);
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(guildName, connection));
        }

        private async Task ProcessMessage(string guildName, UserConnection playerConn)
        {            
            if (guildName.Length < 2)
            {
                byte[] errMsg = MergeByteArrays(ToBytes(RpcType.RpcGuildCreate), ToBytes(false)); // sending false to signify "failure"
                playerConn.Send(errMsg);
                return;
            }

            var player = Server!.GameLogic.GetPlayerByConnection(playerConn);
            if (player == null) return;
            if (player.GuildId != -1) return; // make sure the player isn't in a guild yet
            
            var guild = await Server!.Database.CreateGuild(guildName, player.CharId);
            if (guild == null)
            {                
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcGuildCreate), ToBytes(false)); // send message that we failed
                playerConn.Send(msgFail);
                return;
            }
            Console.WriteLine($"{DateTime.Now:HH:mm} {player.Name} has created a guild \"{guildName}\"");
            Server!.GameLogic.CreateGuild(guild, player);

            // send message to guild leader that he succeeded in creating the guild
            byte[] msgToClient = MergeByteArrays(ToBytes(RpcType.RpcGuildCreate), ToBytes(true));
            playerConn.Send(msgToClient);

            // send the newly created guild roster to all the members (in this case just one character to himself)
            byte[] msgNewRoster = MergeByteArrays(ToBytes(RpcType.RpcGuildAllMembersUpdate), WriteMmoString(GetGuildMembersJson(guild)));
            foreach (var member in guild.GetOnlineMembers())
            {
                member.Conn.Send(msgNewRoster);
            }

            // send message to all servers that a character with a certain id belongs to a guild with a certain id & name
            // For server, params are: char id, guild name, guild id, guild rank
            byte[] msgToServers = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(player.CharId), WriteMmoString(guild.Name), ToBytes(guild.Id), ToBytes(player.GuildRank));
            foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
            {
                serverConn.Send(msgToServers);
            }
        }

        private void SanitizeGuildName(ref string guildName)
        {
            guildName = Regex.Replace(guildName, "[\\p{S}\\p{C}\\p{P}]", ""); // removes punctuation, symbols and control characters
            guildName = Regex.Replace(guildName, @"\s+", " "); // replace repetitive whitespace characters with just one whitespace
        }
    }
}