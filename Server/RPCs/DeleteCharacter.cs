using System.Numerics;

namespace PersistenceServer.RPCs
{
    internal class DeleteCharacter : BaseRpc
    {
        public DeleteCharacter()
        {
            RpcType = RpcType.RpcDeleteCharacter; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string charName = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(charName, connection));
        }

        private async Task ProcessMessage(string charName, UserConnection playerConn)
        {
            int accountId = Server!.GameLogic.GetAccountId(playerConn);
            if (accountId == -1)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Unlogged user attempted to delete a character? This must never happen.");
                _ = playerConn.Disconnect(); // not awaited
                return;
            }

            var onlinePlayer = Server!.GameLogic.GetPlayerByConnection(playerConn);
            if (onlinePlayer != null && onlinePlayer.Name == charName)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} WARNING: Account {accountId} attempted to delete ONLINE character {charName} from DB, refusing!");
                return;
            }

            var characterInDb = await Server!.Database.GetCharacterByName(charName, accountId);
            if (characterInDb == null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} WARNING: Account {accountId} attempted to delete character {charName} from DB, but doesn't own it!");
                return;
            }

            var success = await Server!.Database.DeleteCharacter(charName, accountId);
            if (!success) 
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} WARNING: Account {accountId} attempted to delete character {charName} from DB, but something went wrong!");
                return;
            }
            
            Console.WriteLine($"{DateTime.Now:HH:mm} Character {charName} was deleted from DB.");

            // Simulate that the owner requested characters
            byte[] emptyMsg = Array.Empty<byte>();
            BinaryReader reader = new(new MemoryStream(emptyMsg));
            Server.InvokeOnMessageReceived(RpcType.RpcGetCharacters, playerConn, reader);

            // If character was in a guild 
            if (characterInDb.Guild == null) return;
            var guild = Server!.GameLogic.GetGuildById((int)characterInDb.Guild);
            if (guild == null) return;

            // delete character from guild
            Server!.GameLogic.DeleteGuildMember(guild, characterInDb.CharId);

            //but he was the only member, then just delete the guild
            if (guild.MembersCount == 0)
            {
                await Server!.Database.DeleteGuild(guild.Id);
                Server!.GameLogic.DeleteGuild(guild.Id);
                // no further actions are necessary, because no guild members can be online in this scenario, so we don't need to inform anyone
            }
            // if player was in a guild and wasn't the only guild member, send a message to all online members that the character left the guild.
            else
            {
                // if player leaving the guild was a guild master, we need to assign a new guild master
                // we'll find a player in this guild with the lowest rank (lower is better, GM is rank 0) and make him the new GM
                // if there's multiple players with the lowest rank, we'll pick one of them at random
                if (characterInDb.GuildRank == 0)
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
                byte[] msgCharLeft = MergeByteArrays(ToBytes(RpcType.RpcGuildLeave), ToBytes(false), WriteMmoString(charName)); // false means it's another player who left, and the string is his name
                foreach (var member in guild.GetOnlineMembers())
                {
                    member.Conn.Send(msgNewRoster);
                    member.Conn.Send(msgCharLeft);
                }
            }
            
        }
    }
}