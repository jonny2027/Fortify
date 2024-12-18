namespace PersistenceServer.RPCs
{
    internal class GuildInvite : BaseRpc
    {
        public GuildInvite()
        {
            RpcType = RpcType.RpcGuildInvite; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string charName = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(charName, connection));
        }

        // 0. check if sender has authority to invite to a guild (is guild officer)
        // 1. check if target player is online
        // 2. check if target player is guildless
        // 3. check if target player has a pending invite
        // 3. send the invite to player if all is clear
        // 4. send confirmation to sender if all is clear
        private void ProcessMessage(string recipientName, UserConnection senderConn)
        {
            var sender = Server!.GameLogic.GetPlayerByConnection(senderConn);
            if (sender == null) return; // player could've disconnected in the meantime, so we abort
            var guild = Server!.GameLogic.GetPlayerGuild(senderConn);
            if (guild == null) return;
            if (sender.GuildRank > Server.Settings.GuildOfficerRank) return; // only Officer can invite
            var senderName = sender.Name;

            // attempt to find recipient
            var recipient = Server!.GameLogic.GetPlayerByName(recipientName);
            if (recipient == null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Guild Invite from {senderName} to {recipientName} failed: no player found");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcNoSuchPlayer)); // send "no such player"
                senderConn.Send(msgFail);
                return;
            }

            if (recipient.GuildId != -1)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Guild Invite from {senderName} to {recipientName} failed: player is already in a guild");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcGuildInvite), ToBytes(false), ToBytes(0)); // send "player is in a guild"
                senderConn.Send(msgFail);
                return;
            }

            if (recipient.HasPendingInvite())
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Guild Invite from {senderName} to {recipientName} failed: player has a pending invite");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcGuildInvite), ToBytes(false), ToBytes(1)); // send "player is busy"
                senderConn.Send(msgFail);
                return;
            }

            Console.WriteLine($"{DateTime.Now:HH:mm} Guild Invite from {senderName} to {recipientName} was sent");

            recipient.SetInviteToGuild(sender.Name, sender.GuildId);

            byte[] msgToRecipient = MergeByteArrays(ToBytes(RpcType.RpcGuildInvite), ToBytes(true), WriteMmoString(senderName), WriteMmoString(guild.Name)); // send "X invites you to guild Y"
            recipient.Conn.Send(msgToRecipient);

            byte[] msgConfirmToSender = MergeByteArrays(ToBytes(RpcType.RpcGuildInvite), ToBytes(false), ToBytes(2)); // send "invite sent successfully"
            senderConn.Send(msgConfirmToSender);
        }
    }
}