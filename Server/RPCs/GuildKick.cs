using System.Numerics;
using System.Reflection;

namespace PersistenceServer.RPCs
{
    internal class GuildKick : BaseRpc
    {
        public GuildKick()
        {
            RpcType = RpcType.RpcGuildKick; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string charName = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(charName, connection));
        }

        private async Task ProcessMessage(string kickedName, UserConnection playerConn)
        {
            var kickInitiator = Server!.GameLogic.GetPlayerByConnection(playerConn);
            if (kickInitiator == null) return;
            var guild = Server!.GameLogic.GetPlayerGuild(playerConn);
            if (guild == null) return;
            if (kickInitiator.GuildRank > Server.Settings.GuildOfficerRank) return; // only Officer can kick
            

            var kickedGuildMember = guild.GetGuildMemberByName(kickedName);
            if (kickedGuildMember == null) return; // no player with this name found in the guild, maybe he's not in this guild or not in any guild

            if (kickedGuildMember.GuildRank <= kickInitiator.GuildRank) return; // can't kick members of equal standing or with more powerful rank

            Console.WriteLine($"{DateTime.Now:HH:mm} {kickInitiator.Name} has kicked {kickedGuildMember.MemberName} out of the guild.");

            // can be null if player isn't online
            var kickedPlayerOnline = Server!.GameLogic.GetPlayerByName(kickedName);

            // remove
            await Server!.Database.RemoveGuildMember(kickedGuildMember.Id);
            Server!.GameLogic.RemoveGuildMember(guild, kickedGuildMember.Id, kickedPlayerOnline);
            
            if (kickedPlayerOnline != null)
            {
                // send message to game servers that a character with a certain id is now guildless
                // For server, params are: char id, guild name, guild id, guild rank
                byte[] msgToServers = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(kickedPlayerOnline.CharId), WriteMmoString(""), ToBytes(-1), ToBytes(-1));
                foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
                {
                    // once the server updates the character's guildId and it replicates to client, the client will display a guild window as guildless
                    serverConn.Send(msgToServers);
                }
            }

            // replicate to all members the message about a kick
            // replicate to all members a new roster
            byte[] msgNewRoster = MergeByteArrays(ToBytes(RpcType.RpcGuildAllMembersUpdate), WriteMmoString(GetGuildMembersJson(guild)));
            byte[] msgCharLeft = MergeByteArrays(ToBytes(RpcType.RpcGuildKick), ToBytes(true), WriteMmoString(kickedGuildMember.MemberName)); // true means it's another player and not you
            foreach (var member in guild.GetOnlineMembers())
            {
                member.Conn.Send(msgNewRoster);
                member.Conn.Send(msgCharLeft);
            }

            // if player is online, send him a message that he was kicked
            if (kickedPlayerOnline != null)
            {
                byte[] msgToKicked = MergeByteArrays(ToBytes(RpcType.RpcGuildKick), ToBytes(false), WriteMmoString(kickedGuildMember.MemberName)); // false means it's you who were kicked
                kickedPlayerOnline.Conn.Send(msgToKicked);
            }
        }
    }
}