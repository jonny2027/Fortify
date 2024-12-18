namespace PersistenceServer.RPCs
{
    public class CreateAccountPassword : BaseRpc
    {
        public CreateAccountPassword()
        {
            RpcType = RpcType.RpcCreateAccountPassword; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string accountName = reader.ReadMmoString();
            string password = reader.ReadMmoString();
#if DEBUG
            Console.WriteLine($"(thread {Environment.CurrentManagedThreadId}): Create account");
#endif            
            Server!.Processor.ConQ.Enqueue(async () => await ProcessAccountCreation(accountName, password, connection));
        }

        private async Task ProcessAccountCreation(string accountName, string password, UserConnection connection)
        {
            // if account doesn't exist, create it
            if (!await Server!.Database.DoesAccountExist(accountName))
            {
                Console.Write($"Creating account for user: '{accountName}', ");
                int accountId = await Server!.Database.CreateUserAccount(accountName, password);
                Console.WriteLine($"id: {accountId}");

                // don't ask the user to log in after creating the account, consider him logged in
                var cookie = BCrypt.Net.BCrypt.GenerateSalt();
                Server!.GameLogic.UserLoggedIn(accountId, cookie, connection);

                // sending true to signify "success", plus a cookie
                // the cookie will allow a reconnection later, when the user changes the level to enter the game server
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcCreateAccountPassword), ToBytes(true), WriteMmoString(cookie)); // sending true to signify "success"
                connection.Send(msg);
            }
            // if account exists, tell user to pick a different account name
            else
            {
                Console.WriteLine($"Creating account for user '{accountName}' failed: username taken");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcCreateAccountPassword), ToBytes(false)); // sending false to signify "failure"
                connection.Send(msg);
            }
        }
    }
}