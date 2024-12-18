using System.Numerics;

namespace PersistenceServer.RPCs
{
    internal class GuildAdjustRank : BaseRpc
    {
        public GuildAdjustRank()
        {
            RpcType = RpcType.RpcGuildAdjustRank; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            bool increaseRank = reader.ReadBoolean();
            string message = reader.ReadMmoString();
            int maxLength = 255;
            // Trim message by maxLength (255 characters)
            message = message.Length <= maxLength ? message : message[..maxLength]; // .. is a C# 8.0 Range Operator https://www.codeguru.com/csharp/c-8-0-ranges-and-indices-types/            
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(message, increaseRank, connection));
        }

        private async Task ProcessMessage(string victimName, bool increaseRank, UserConnection connection)
        {
            var adjustInitiator = Server!.GameLogic.GetPlayerByConnection(connection);
            if (adjustInitiator == null) return;
            var guild = Server!.GameLogic.GetPlayerGuild(connection);
            if (guild == null) return;
            if (adjustInitiator.GuildRank > Server.Settings.GuildOfficerRank) return; // only Officer can adjust


            var adjustVictim = guild.GetGuildMemberByName(victimName);
            if (adjustVictim == null) return; // no player with this name found in the guild, maybe he's not in this guild or not in any guild

            int newRank = increaseRank ? adjustVictim.GuildRank - 1 : adjustVictim.GuildRank + 1; // increase rank means -1, because the lower, the higher the rank

            // can't adjust rank on members of equal standing or with more powerful rank
            // with one exception: you can demote yourself
            if (adjustVictim.GuildRank <= adjustInitiator.GuildRank)
            {
                if (increaseRank || adjustInitiator.CharId != adjustVictim.Id) return;
            }

            // can't lower the rank beyond DefaultGuildRank
            if (newRank > Server.Settings.DefaultGuildRank || newRank < 0)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} {adjustInitiator.Name} has attempted set rank outside of allowed bounds (from 0 to DefaultGuildRank): rejecting request");
                return; 
            }

            // a GM can't demote self when there's only one GM in a guild - it would result in a GMless guild
            if (adjustInitiator.CharId == adjustVictim.Id && !increaseRank && adjustInitiator.GuildRank == 0 && guild.GuildMastersCount == 1) return;

            string verb = increaseRank ? "promoted" : "demoted";
            Console.WriteLine($"{DateTime.Now:HH:mm} {adjustInitiator.Name} has {verb} {adjustVictim.MemberName} in the guild.");

            // can be null if player isn't online
            var adjustVictimOnline = Server!.GameLogic.GetPlayerByName(victimName);

            // adjust rank in the database
            await Server!.Database.UpdateGuildRank(adjustVictim.Id, newRank);
            // adjust rank on persistent server
            guild.UpdateMemberRank(adjustVictim.Id, newRank);

            // if player is online, adjust it on online player and send message to all servers that this player has a new rank
            if (adjustVictimOnline != null)
            {
                adjustVictimOnline.GuildRank = newRank;
                // For server, params are: char id, guild name, guild id, guild rank
                byte[] updateRankMsg = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(adjustVictimOnline.CharId), WriteMmoString(guild.Name), ToBytes(guild.Id), ToBytes(newRank));
                foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
                {
                    serverConn.Send(updateRankMsg);
                }
            }


            // send two messages to all guild members (clients): 1. update guild member (which includes rank
            // 2. just a message that someone was promoted/demoted for displaying in the chat box
            byte[] msgMemberUpdate = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(adjustVictim.Id), ToBytes(adjustVictim.GuildRank), ToBytes(adjustVictimOnline == null ? false : true));
            byte[] msgCharRankAdjusted = MergeByteArrays(ToBytes(RpcType.RpcGuildAdjustRank), ToBytes(increaseRank), WriteMmoString(adjustVictim.MemberName));
            foreach (var member in guild.GetOnlineMembers())
            {
                member.Conn.Send(msgMemberUpdate);
                member.Conn.Send(msgCharRankAdjusted);
            }
        }
    }
}