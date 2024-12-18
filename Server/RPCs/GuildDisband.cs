namespace PersistenceServer.RPCs
{
    internal class GuildDisband : BaseRpc
    {
        public GuildDisband()
        {
            RpcType = RpcType.RpcGuildDisband; // set it to the RpcType you want to catch
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
            // remove all members from the guild
            // delete the guild from db

            var player = Server!.GameLogic.GetPlayerByConnection(playerConn);
            if (player == null) return;
            var guild = Server!.GameLogic.GetPlayerGuild(playerConn);
            if (guild == null) return;

            if (player.GuildRank != 0)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} {player.Name} tried to DISBAND the guild, but is not a guild leader!");
                return;
            }

            if (guild.GuildMastersCount > 1)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} {player.Name} tried to DISBAND the guild, but he's not the only GM, so the request was rejected.");
                return;
            }

            // update the character's "guild" and "guildrank" in the database, plus delete the guild itself
            await Server!.Database.DisbandGuild(guild.Id);
            // delete the guild from guildlogic
            Server!.GameLogic.DeleteGuild(guild.Id);

            // make a list of characters that need to be made guildless on ue5 servers
            List<int> affectedCharacterIds = new();
            foreach (var member in guild.GetOnlineMembers())
            {
                affectedCharacterIds.Add(member.CharId);
                member.GuildRank = -1;
                member.GuildId = -1;
            }            

            // send message to game servers that these characters are now guildless
            byte[] msgToServers = MergeByteArrays(ToBytes(RpcType.RpcGuildDisband), ToBytes(affectedCharacterIds.Count), ToBytes(affectedCharacterIds.ToArray()));
            foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
            {
                // once the server updates the character's guildId and it replicates to client, the clients will display the guild window as guildless
                serverConn.Send(msgToServers);
            }

            // send message to members that the guild was disbanded (mostly for a system message)
            byte[] msgToPlayers = MergeByteArrays(ToBytes(RpcType.RpcGuildDisband));
            foreach (var formerMember in guild.GetOnlineMembers())
            {
                formerMember.Conn.Send(msgToPlayers);
            }
            Console.WriteLine($"{DateTime.Now:HH:mm} {player.Name} has DISBANDED the guild \"{guild.Name}\"");
        }
    }
}