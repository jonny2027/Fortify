namespace PersistenceServer.RPCs
{
    internal class GuildLeave : BaseRpc
    {
        public GuildLeave()
        {
            RpcType = RpcType.RpcGuildLeave; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(connection));
        }

        private async Task ProcessMessage(UserConnection playerConn)
        {
            // if not in a guild, return
            // if guild master, leave the guild, but also make someone else guild master
            // if just member, just leave the guild
            // if the guild has no members afterwards, disband the guild

            var player = Server!.GameLogic.GetPlayerByConnection(playerConn);
            if (player == null) return;
            var guild = Server!.GameLogic.GetPlayerGuild(playerConn);
            if (guild == null) return;

            bool wasPlayerGuildMaster = player.GuildRank == 0;

            // update the database that this character is now guildless
            await Server!.Database.PlayerLeavesGuild(player.CharId);
            // update game data that this character is now guildless
            player.GuildId = -1;
            player.GuildRank = -1;
            guild.RemoveMember(player);

            // send message to game servers that a character with a certain id is now guildless
            // For server, params are: char id, guild name, guild id, guild rank
            byte[] msgToServers = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(player.CharId), WriteMmoString(""), ToBytes(-1), ToBytes(-1));
            foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
            {
                // once the server updates the character's guildId and it replicates to client, the client will display a guild window as guildless
                serverConn.Send(msgToServers);
            }

            // send message to leaver that he left (mostly for a system message)
            byte[] msgToOwner = MergeByteArrays(ToBytes(RpcType.RpcGuildLeave), ToBytes(true)); // true means it's YOU who left, not other members
            player.Conn.Send(msgToOwner);

            Console.WriteLine($"{DateTime.Now:HH:mm} {player.Name} has left the guild \"{guild.Name}\"");

            // if there's no more members in this guild, simply delete it from the database
            if (guild.MembersCount == 0)
            {
                await Server!.Database.DeleteGuild(guild.Id);
                Server!.GameLogic.DeleteGuild(guild.Id);
                return;
            }

            // if player leaving the guild was a guild master AND there were no other guild masters, we need to assign a new guild master
            // we'll find a player in this guild with the lowest rank (lower is better, GM is rank 0) and make him the new GM
            // if there's multiple players with the lowest rank, we'll pick one of them at random
            if (wasPlayerGuildMaster && guild.GuildMastersCount == 0)
            {
                // set the rank for the new GM and send it to servers, in case he's online and needs to update his guild widget to display guild master's tools
                var newGuildMasterId = await Server!.Database.MakeNewGuildMaster(guild.Id);                
                guild.UpdateMemberRank(newGuildMasterId, 0);
                // For server, params are: char id, guild name, guild id, guild rank
                byte[] updateRankMsg = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(newGuildMasterId), WriteMmoString(guild.Name), ToBytes(guild.Id), ToBytes(0));
                foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
                {
                    serverConn.Send(updateRankMsg);
                }
            }

            // replicate to all remaining guild members a new list of guild members
            // if you wish to optimize it, you could do it in an additive way, i.e. just send a "MemberLeft" and his id, same with MemberJoined, MemberOnline, MemberOffline, and RankUpdate
            // but I don't think it's worth the trouble
            byte[] msgNewRoster = MergeByteArrays(ToBytes(RpcType.RpcGuildAllMembersUpdate), WriteMmoString(GetGuildMembersJson(guild)));
            byte[] msgCharLeft = MergeByteArrays(ToBytes(RpcType.RpcGuildLeave), ToBytes(false), WriteMmoString(player.Name)); // false means it's another player who left, and the string is his name
            foreach (var member in guild.GetOnlineMembers())
            {
                member.Conn.Send(msgNewRoster);
                member.Conn.Send(msgCharLeft);
            }            
        }
    }
}