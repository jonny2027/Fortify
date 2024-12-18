namespace PersistenceServer.RPCs
{
    public class MessagePlayer : BaseRpc
    {
        public MessagePlayer()
        {
            RpcType = RpcType.RpcMessagePlayer; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string recipient = reader.ReadMmoString();
            string message = reader.ReadMmoString();
            bool talkAsGM = reader.ReadBoolean();
            int maxLength = 255;
            // Trim message by maxLength (255 characters)
            message = message.Length <= maxLength ? message : message[..maxLength];
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(recipient, message, talkAsGM, connection));
        }

        private void ProcessMessage(string recipientName, string message, bool talkAsGM, UserConnection senderConn)
        {
            var sender = Server!.GameLogic.GetPlayerByConnection(senderConn);
            if (sender == null) return; // player could've disconnected in the meantime, so we abort
            var senderName = sender.Name;

            // if talkAsGM is true, but sender is not a GM, simply reset it to false
            if (talkAsGM && !sender.IsGm())
            {
                talkAsGM = false;
            }
            string GM = talkAsGM ? "<GM>" : "";

            // attempt to find recipient
            var recipient = Server!.GameLogic.GetPlayerByName(recipientName);
            // if recipient not found
            if (recipient == null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} [Private (failed)] {GM}{senderName} to {recipientName}: \"{message}\"");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcNoSuchPlayer));
                senderConn.Send(msgFail);
                return;
            }

            Console.WriteLine($"{DateTime.Now:HH:mm} [Private] {GM}{senderName} to {recipientName}: \"{message}\"");

            byte[] msgToSender = MergeByteArrays(
                ToBytes(RpcType.RpcMessagePlayer), 
                ToBytes(true), // true to signify "it's your message"
                WriteMmoString(recipientName), 
                WriteMmoString(message),
                ToBytes(talkAsGM)
            );
            senderConn.Send(msgToSender);

            byte[] msgToRecipient = MergeByteArrays(
                ToBytes(RpcType.RpcMessagePlayer),
                ToBytes(false), // true to signify "it's not your message"
                WriteMmoString(senderName),
                WriteMmoString(message),
                ToBytes(talkAsGM)
            );
            recipient.Conn.Send(msgToRecipient);            
        }
    }
}