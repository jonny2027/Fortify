using System;
using System.Numerics;

namespace PersistenceServer.RPCs
{
    internal class AcceptInvite : BaseRpc
    {
        public AcceptInvite()
        {
            RpcType = RpcType.RpcAcceptInvite; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            bool accept = reader.ReadBoolean();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(accept, connection));
        }

        private async Task ProcessMessage(bool accept, UserConnection senderConn)
        {
            var invitedPlayer = Server!.GameLogic.GetPlayerByConnection(senderConn);
            if (invitedPlayer == null) return;
            if (!invitedPlayer.HasPendingInvite()) return; // timeout happened

            PlayerInvitation invite = invitedPlayer.GetPendingInvite()!;

            var inviter = Server!.GameLogic.GetPlayerByName(invite.inviterName);
            if (inviter != null) // inviter may have disconnected in the meantime
            {
                byte[] msgToInviter = MergeByteArrays(ToBytes(RpcType.RpcAcceptInvite), ToBytes(accept), WriteMmoString(invitedPlayer.Name));
                inviter.Conn.Send(msgToInviter);
            }

            if (accept && invite is GuildInvitation guildInvite)
            {
                var guild = Server!.GameLogic.GetGuildById(guildInvite.guildId);

                // if guild got disbanded in the meantime, just return
                // or if player isn't guildless, return (e.g. joined another guild in the meantime by creating one)
                if (guild == null || invitedPlayer.GuildId != -1)
                {
                    invitedPlayer.ClearPendingInvite();
                    return;
                }

                Console.WriteLine($"{DateTime.Now:HH:mm} {invitedPlayer.Name} joins the guild {guild.Name}");

                int rank = Server.Settings.DefaultGuildRank;                
                await Server!.Database.AddGuildMember(guildInvite.guildId, invitedPlayer.CharId, rank);
                Server!.GameLogic.AddGuildMember(guild, invitedPlayer, rank);

                // send the newly created guild roster to all the members
                byte[] msgNewRoster = MergeByteArrays(ToBytes(RpcType.RpcGuildAllMembersUpdate), WriteMmoString(GetGuildMembersJson(guild)));
                byte[] msgPlayerJoined = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberJoined), WriteMmoString(invitedPlayer.Name));
                foreach (var member in guild.GetOnlineMembers())
                {
                    member.Conn.Send(msgNewRoster);
                    // send "player joined your guild" to all online members except to the joining player
                    if (member != invitedPlayer)
                    {
                        member.Conn.Send(msgPlayerJoined);
                    }
                }

                // send message to all servers that a character with a certain id belongs to a guild with a certain id & name
                // For server, params are: char id, guild name, guild id, guild rank
                byte[] msgToServers = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(invitedPlayer.CharId), WriteMmoString(guild.Name), ToBytes(guild.Id), ToBytes(invitedPlayer.GuildRank));
                foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
                {
                    serverConn.Send(msgToServers);
                }
                            
            }

            invitedPlayer.ClearPendingInvite();
        }
    }
}