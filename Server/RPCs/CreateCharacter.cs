using Newtonsoft.Json.Linq;

namespace PersistenceServer.RPCs
{
    public class CreateCharacter : BaseRpc
    {
        public CreateCharacter()
        {
            RpcType = RpcType.RpcCreateCharacter; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string playerName = reader.ReadMmoString();
            string serializedCharacter = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessCharacterCreation(playerName, serializedCharacter, connection));
        }

        private async Task ProcessCharacterCreation(string playerName, string serializedCharacter, UserConnection connection)
        {
            if (!IsNamedAllowed(ref playerName))
            {
                Console.WriteLine($"Creating player named '{playerName}' failed: invalid charname");
                byte[] errMsg = MergeByteArrays(ToBytes(RpcType.RpcCreateCharacter), ToBytes(false)); // sending false to signify "failure"
                connection.Send(errMsg);
                return;
            }
            if (await Server!.Database.DoesCharnameExist(playerName))
            {
                Console.WriteLine($"Creating player named '{playerName}' failed: charname taken");
                byte[] errMsg = MergeByteArrays(ToBytes(RpcType.RpcCreateCharacter), ToBytes(false)); // sending false to signify "failure"
                connection.Send(errMsg);
                return;
            }

            // Check if 'NewCharacter' is present in serializedCharacter and is true
            // Otherwise, an exploit is possible: a tampered packet could add a fully levelled up, fully equipped character into the database
            JObject jsonObject = JObject.Parse(serializedCharacter);
            if (!jsonObject.TryGetValue("NewCharacter", out JToken? value) || value.Type != JTokenType.Boolean || !value.ToObject<bool>())
            {
                Console.WriteLine("The 'NewCharacter' field is not present or not true in character creation packet. Player attempted to cheat.");
                byte[] errMsg = MergeByteArrays(ToBytes(RpcType.RpcCreateCharacter), ToBytes(false)); // sending false to signify "failure"
                connection.Send(errMsg);
                return;
            }

            int accountId = Server!.GameLogic.GetAccountId(connection);
            if (accountId == -1)
            {
                Console.WriteLine("Creating player failed: user not logged in. This must never happen.");
                _ = connection.Disconnect(); // not awaited
                return;
            }

            bool gmCharacter = await Server!.Database.IsCharactersTableEmpty();
            if (gmCharacter)
            {
                Console.WriteLine($"No existing characters in the DB. Creating a GM character.");
            }

            Console.Write($"Creating character named: '{playerName}', ");
            int playerId = await Server!.Database.CreateCharacter(playerName, accountId, gmCharacter, serializedCharacter);
            Console.WriteLine($"id: {playerId}");

            byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcCreateCharacter), ToBytes(true)); // sending false to signify "success"
            connection.Send(msg);
            
        }

        //@TODO for the developer: implement various name checks, e.g. use a blacklist
        private bool IsNamedAllowed(ref string playerName)
        {
            if (playerName.Length < 2) return false;
            var words = playerName.Split(' ');
            if (words.Length > 2) return false;
            if (words.Length < 1) return false;
            foreach (var word in words)
            {
                // check length 2-15
                if (word.Length > 15) return false;
                if (word.Length < 2) return false;

                // check if it's only letters
                if (!word.All(char.IsLetter)) return false;                                
            }

            // all checks passed
            // correct case: the first must be upper, but the rest must be lower case            
            var word1 = words[0].ToLower();
            word1 = string.Concat(word1[0].ToString().ToUpper(), word1.AsSpan(1));

            playerName = word1;

            if (words.Length == 2)
            {
                var word2 = words[1].ToLower();
                word2 = string.Concat(word2[0].ToString().ToUpper(), word2.AsSpan(1));
                playerName += " " + word2;
            }
            return true;
        }
    }
}