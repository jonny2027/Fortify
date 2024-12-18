using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

namespace PersistenceServer
{
    public class DatabaseCharacterInfo
    {
        public int AccountId;
        public int CharId;
        public string Name = "";
        public int Permissions;
        public string SerializedCharacter = "";
        public int? Guild;
        public int? GuildRank;        
    }

    public abstract class Database
    {
        protected string ConnectionParams;
        // Change Pepper to any random string before creating any user accounts. Once users are created, don't ever change it again.
        protected readonly string Pepper = "$2a$11$46Z/ZIevW5fGpZFXJK5CMe";
        protected string GetIdentitySqlCommand;

#pragma warning disable CS8618, IDE0060 // ignore CS8618 because it's an abstract class, IDE0060 because we're using the settings in children classes
        protected Database(SettingsReader settings) { }
#pragma warning restore CS8618, IDE0060

        // Checks that there is a database, if not creates one
        // Overridden in SQLite and MySQL implementations
        public abstract Task CheckCreateDatabase(SettingsReader settings);
        // Overridden in SQLite and MySQL implementations
        public abstract Task<Guild?> CreateGuild(string guildName, int charId);
        // Overridden in SQLite and MySQL implementations
        public abstract Task SaveServerInfo(string serializedServerInfo, int port, string level);
        public abstract Task SavePersistentObject(string level, int port, int objectId, string jsonString);
        // Always call with 'using' keyword or close manually
        protected abstract Task<DbConnection> GetConnection(string parameters);
        protected abstract DbCommand GetCommand(string parameters, DbConnection? connection);
        protected virtual DbCommand GetCommand(string parameters) => GetCommand(parameters, null);

        protected async Task<DataTable> RunQuery(string cmdParams, string overrideConnectionParams)
        {
            await using var conn = await GetConnection(overrideConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            DataTable dt = new();
            dt.Load(reader);
            return dt;
        }

        protected async Task<DataTable> RunQuery(string cmdParams)
        {
            await using var conn = await GetConnection(ConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            DataTable dt = new();
            dt.Load(reader);
            return dt;
        }

        protected async Task<DataTable> RunQuery(DbCommand command)
        {
            await using var conn = await GetConnection(ConnectionParams);
            command.Connection = conn;
            await using var reader = await command.ExecuteReaderAsync();
            DataTable dt = new();
                dt.Load(reader);
            await command.DisposeAsync();
            return dt;
        }

        // Returns the number of rows affected
        protected async Task<int> RunNonQuery(string cmdParams, string overrideConnectionParams)
        {
            await using var conn = await GetConnection(overrideConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            return await cmd.ExecuteNonQueryAsync();
        }

        // Returns the number of rows affected
        protected async Task<int> RunNonQuery(string cmdParams)
        {
            await using var conn = await GetConnection(ConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            return await cmd.ExecuteNonQueryAsync();
        }

        // Returns the number of rows affected
        protected async Task<int> RunNonQuery(DbCommand command)
        {
            await using var conn = await GetConnection(ConnectionParams);
            command.Connection = conn;
            int rowsAffected = await command.ExecuteNonQueryAsync();
            await command.DisposeAsync();
            return rowsAffected;
        }

        //protected async Task<int> RunScalar(string cmdParams, string overrideConnectionParams)
        //{
        //    await using var conn = await GetConnection(overrideConnectionParams);
        //    await using var cmd = GetCommand(cmdParams, conn);
        //    await cmd.ExecuteNonQuery();
        //}

        //protected async Task<int> RunScalar(string cmdParams)
        //{
        //    await using var conn = await GetConnection(ConnectionParams);
        //    await using var cmd = GetCommand(cmdParams, conn);
        //    await cmd.ExecuteNonQuery();
        //}

        // Returns the last inserted row id
        protected async Task<int> RunInsert(DbCommand command)
        {
            command.CommandText += GetIdentitySqlCommand;
            await using var conn = await GetConnection(ConnectionParams);
            command.Connection = conn;
            object? obj = await command.ExecuteScalarAsync();
            await command.DisposeAsync();
            return (int)Convert.ChangeType(obj!, typeof(int));
        }

        public async Task HelloWorld()
        {
            //var dt = await RunQuery(@"SET @helloWorldStr = ""Hello World!"";SELECT @helloWorldStr AS ""My Hello World"";");
            var cmd = GetCommand(@"SET @helloWorldStr = ""Hello World!"";SELECT @helloWorldStr AS ""My Row"";");
            var dt = await RunQuery(cmd);
            Debug.Assert(dt.HasRows());            
            Console.WriteLine(dt.Rows[0]["My Row"].ToString());            
        }

        /******************** Below are gameplay related DB requests ****************/

        public virtual async Task<bool> DoesAccountExist(string accountName)
        {
            var cmd = GetCommand("SELECT * FROM accounts WHERE name = @accountName");
            cmd.AddParam("@accountName", accountName);
            var dt = await RunQuery(cmd);
            return dt.HasRows();
        }

        public virtual async Task<int> CreateUserAccount(string accountName, string password)
        {
            string salt = BCrypt.Net.BCrypt.GenerateSalt();
            var cmd = GetCommand("INSERT INTO `accounts` (`id`, `name`, `steamid`, `password`, `salt`, `email`, `status`) VALUES (NULL, @accountName, NULL, @password, @salt, NULL, 0);");
            cmd.AddParam("@accountName", accountName);
            cmd.AddParam("@password", BCrypt.Net.BCrypt.HashPassword(password, salt + Pepper));
            cmd.AddParam("@salt", salt);
            int lastInsertedId = await RunInsert(cmd);
            return lastInsertedId;
        }

        public virtual async Task<int> CreateSteamAccount(string steamId)
        {
            var cmd = GetCommand("INSERT INTO `accounts` (`id`, `name`, `steamid`, `password`, `salt`, `email`, `status`) VALUES (NULL, NULL, @steamid, NULL, NULL, NULL, 0);");
            cmd.AddParam("@steamid", steamId);
            int lastInsertedId = await RunInsert(cmd);
            return lastInsertedId;
        }

        /* Returns: returns -1 if login failes, otherwise returns the user account's id
         * It's different for Sqlite and MySQL because of how passwords are stored, 
         * though I'm not sure why it's recommended to store BCrypt hashes as binary arrays (sqlite doesn't even have them)
         * If you know something about it, let me know. */
        public abstract Task<int> LoginUser(string accountName, string password);

        public virtual async Task<int> LoginSteamUser(string steamId)
        {
            var cmd = GetCommand("SELECT id, status FROM accounts WHERE steamid = @steamId");
            cmd.AddParam("@steamId", steamId);
            var dt = await RunQuery(cmd);

            // if no account with this steamid is found, we should create it!
            if (!dt.HasRows())
            {
                int newId = await CreateSteamAccount(steamId);
                return newId;
            }
            // if it's found
            else
            {
                var id = (int)dt.GetInt(0, "id")!;
                var status = dt.GetInt(0, "status")!;

                // if status is banned
                if (status == -1)
                {
                    return -1;
                }

                // if everything checks out, allow login by returning user's id
                return id;
            }
        }

        public virtual async Task<bool> DoesCharnameExist(string charName)
        {
            var cmd = GetCommand("SELECT * FROM characters WHERE name = @charName");
            cmd.AddParam("@charName", charName);
            var dt = await RunQuery(cmd);
            return dt.HasRows();
        }

        public virtual async Task<int> CreateCharacter(string charName, int ownerAccountId, bool gmCharacter, string serializedCharacter)
        {
            var cmd = GetCommand("INSERT INTO `characters` (`id`, `name`, `owner`, `guild`, `guildrank`, `permissions`, `serialized`) VALUES (NULL, @charName, @ownerAccountId, NULL, NULL, @permissions, @serialized);");
            cmd.AddParam("@charName", charName);
            cmd.AddParam("@ownerAccountId", ownerAccountId);
            cmd.AddParam("@permissions", gmCharacter ? 10 : 0); // GM gets permissions 10, player gets 0, feel free to change it
            cmd.AddParam("@serialized", serializedCharacter);
            int lastInsertedId = await RunInsert(cmd);
            return lastInsertedId;
        }

        public virtual async Task<List<DatabaseCharacterInfo>> GetCharacters(int accountId)
        {
            List<DatabaseCharacterInfo> result = new();

            var cmd = GetCommand("SELECT * FROM characters WHERE owner = @accountId");
            cmd.AddParam("@accountId", accountId);
            var dt = await RunQuery(cmd);
            foreach (var row in dt.Rows.OfType<DataRow>())
            {
                DatabaseCharacterInfo charInfo = new()
                {
                    AccountId = (int)row.GetInt("owner")!,
                    CharId = (int)row.GetInt("id")!,
                    Name = row.GetString("name")!,
                    Permissions = (int)row.GetInt("permissions")!,
                    SerializedCharacter = row.GetString("serialized")!,
                    Guild = row.GetInt("guild"),
                    GuildRank = row.GetInt("guildrank")
                };
                result.Add(charInfo);
            }

            return result;
        }

        /* Returns: name, permissions, serialized, guild, guildrank */
        public virtual async Task<DatabaseCharacterInfo?> GetCharacter(int charId, int accountId)
        {
            var cmd = GetCommand("SELECT * FROM characters WHERE id = @charId and owner = @accountId");
            cmd.AddParam("@charId", charId);
            cmd.AddParam("@accountId", accountId);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];

            DatabaseCharacterInfo character = new() {
                AccountId = (int)row.GetInt("owner")!,
                CharId = (int)row.GetInt("id")!,
                Name = row.GetString("name")!,
                Permissions = (int)row.GetInt("permissions")!,
                SerializedCharacter = row.GetString("serialized")!,
                Guild = row.GetInt("guild"),
                GuildRank = row.GetInt("guildrank")                
            };
            return character;
        }

        /* Returns: name, permissions, serialized, guild, guildrank */
        public virtual async Task<DatabaseCharacterInfo?> GetCharacterByName(string charName, int accountId)
        {
            var cmd = GetCommand("SELECT * FROM characters WHERE name = @charName and owner = @accountId");
            cmd.AddParam("@charName", charName);
            cmd.AddParam("@accountId", accountId);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];

            DatabaseCharacterInfo character = new() {
                AccountId = (int)row.GetInt("owner")!,
                CharId = (int)row.GetInt("id")!,
                Name = row.GetString("name")!,
                Permissions = (int)row.GetInt("permissions")!,
                SerializedCharacter = row.GetString("serialized")!,
                Guild = row.GetInt("guild"),
                GuildRank = row.GetInt("guildrank")
            };
            return character;
        }

        public virtual async Task<DatabaseCharacterInfo?> GetCharacterForPieWindow(int pieWindowId)
        {
            var cmd = GetCommand("SELECT * FROM `characters` ORDER BY id ASC LIMIT @pieWindowId,1 ");
            cmd.AddParam("@pieWindowId", pieWindowId);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];

            DatabaseCharacterInfo charInfo = new() {
                AccountId = (int)row.GetInt("owner")!,
                CharId = (int)row.GetInt("id")!,
                Name = row.GetString("name")!,
                Permissions = (int)row.GetInt("permissions")!,
                SerializedCharacter = row.GetString("serialized")!,
                Guild = row.GetInt("guild"),
                GuildRank = row.GetInt("guildrank")
            };
            return charInfo;
        }

        public async Task SaveCharacter(int charId, string serializedCharacter)
        {
            var cmd = GetCommand("UPDATE `characters` SET serialized = @serializedChar WHERE id = @charId");
            cmd.AddParam("@charId", charId);
            cmd.AddParam("@serializedChar", serializedCharacter);
            int result = await RunNonQuery(cmd);
            if (result == 1) Console.WriteLine($"{DateTime.Now:HH:mm} Character with id {charId} was saved to DB.");
            else Console.WriteLine($"{DateTime.Now:HH:mm} Character wasn't saved: {charId}!");
        }

        public async virtual Task<Dictionary<int, Guild>> GetGuilds()
        {
            var result = new Dictionary<int, Guild>();

            /*
             * An example of what we can expect in return:
             * 
             * guildId	    guildName			charId		charName 	
             *    1 		Diamond Dogs 		1 			Arthur Pendragon
             *    1         Diamond Dogs        2           Raven
             *    2 		No Dogs 			NULL 		NULL 
             * 
             * In this example "Diamond Dogs" has two members: Arthur Pendragon and Raven
             * The guild "No Dogs" is memberless. It shouldn't happen, but if it does, we'll print a warning.
             */
            var cmd = GetCommand(@"
                SELECT guilds.id as guildId, guilds.name as guildName, characters.id as charId, characters.name as charName, characters.guildRank as guildRank FROM guilds
                LEFT JOIN characters
                ON guilds.id = characters.guild
            ");
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return result;

            foreach (var row in dt.Rows.OfType<DataRow>())
            {
                var guildId = (int)row.GetInt("guildId")!;
                var guildName = (string)row.GetString("guildName")!;
                var charId = row.GetInt("charId");
                var charName = row.GetString("charName");
                var guildRank = row.GetInt("guildRank");
                // if the guild hasn't been initialized yet, do so now
                if (!result.ContainsKey(guildId))
                {
                    result.Add(guildId, new Guild(guildId, guildName));
                }
                if (charId == null || charName == null)
                {
                    Console.WriteLine($"Info: guild \"{guildName}\" (id: {guildId}) is parentless and memberless");
                    continue;
                }
                result[guildId].PopulateMember((int)charId, charName, (int)guildRank!);
            }

            return result;
        }

        public async Task PlayerLeavesGuild(int charId)
        {
            var cmd = GetCommand("UPDATE `characters` SET `guild` = NULL, `guildRank` = NULL WHERE `characters`.`id` = @charId; ");            
            cmd.AddParam("@charId", charId);
            await RunNonQuery(cmd);
        }

        public async Task DeleteGuild(int guildId)
        {
            var cmd = GetCommand("DELETE FROM `guilds` WHERE `guilds`.`id` = @guildId");
            cmd.AddParam("@guildId", guildId);
            await RunNonQuery(cmd);
        }

        public async Task<int> MakeNewGuildMaster(int guildId)
        {
            var cmd = GetCommand("SELECT `id` FROM `characters` where `guild` = @guildId ORDER BY `guildrank` ASC LIMIT 0,1 ");
            cmd.AddParam("@guildId", guildId);
            var dt = await RunQuery(cmd);
            var charId = (int)dt.Rows[0].GetInt("id")!;

            var cmd2 = GetCommand("UPDATE `characters` SET `guildRank` = '0' WHERE `id` = @charId");
            cmd2.AddParam("@charId", charId);
            await RunNonQuery(cmd2);

            return charId;
        }

        public async Task UpdateGuildRank(int charId, int rank)
        {
            var cmd = GetCommand("UPDATE `characters` SET `guildRank` = @rank WHERE `id` = @charId");
            cmd.AddParam("@rank", rank);
            cmd.AddParam("@charId", charId);
            await RunNonQuery(cmd);
        }

        public async Task DisbandGuild(int guildId)
        {
            var cmd = GetCommand("UPDATE `characters` SET `guild` = NULL, `guildRank` = NULL WHERE `guild` = @guildId");
            cmd.AddParam("@guildId", guildId);
            await RunNonQuery(cmd);

            var cmd2 = GetCommand("DELETE FROM `guilds` WHERE `guilds`.`id` = @guildId");
            cmd2.AddParam("@guildId", guildId);
            await RunNonQuery(cmd2);
        }

        public async Task AddGuildMember(int guildId, int memberId, int rank)
        {
            var updateCharCmd = GetCommand("UPDATE `characters` SET `guild` = @guildId, `guildRank` = @rank WHERE `characters`.`id` = @charId; ");
            updateCharCmd.AddParam("@guildId", guildId);
            updateCharCmd.AddParam("@rank", rank);
            updateCharCmd.AddParam("@charId", memberId);
            await RunNonQuery(updateCharCmd);
        }

        public async Task RemoveGuildMember(int memberId)
        {
            var updateCharCmd = GetCommand("UPDATE `characters` SET `guild` = NULL, `guildRank` = NULL WHERE `characters`.`id` = @charId; ");
            updateCharCmd.AddParam("@charId", memberId);
            await RunNonQuery(updateCharCmd);
        }

        public async Task<bool> DeleteCharacter(string charName, int accountId)
        {
            var deleteCharCmd = GetCommand("DELETE FROM `characters` WHERE `name` = @charName AND `owner` = @accountId;");
            deleteCharCmd.AddParam("@charName", charName);
            deleteCharCmd.AddParam("@accountId", accountId);
            int result = await RunNonQuery(deleteCharCmd);
            return (result == 1);
        }

        /* Returns: serialized json string */
        public virtual async Task<string?> GetServerInfo(int port, string level)
        {
            var cmd = GetCommand("SELECT serialized FROM servers WHERE port = @port and level = @level");
            cmd.AddParam("@port", port);
            cmd.AddParam("@level", level);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];
            return row.GetString("serialized");
        }

        public struct PersistentObject
        {
            public int ObjectId;
            public string JsonString;
            public PersistentObject(int inObjectId, string inJsonString)
            {
                ObjectId = inObjectId;
                JsonString = inJsonString;
            }
        }

        public virtual async Task<List<PersistentObject>> GetPersistentObjects(int port, string level)
        {
            List<PersistentObject> result = new();
            var cmd = GetCommand("SELECT objectId, serialized FROM persistentobjs WHERE port = @port and level = @level");
            cmd.AddParam("@port", port);
            cmd.AddParam("@level", level);
            var dt = await RunQuery(cmd);
            foreach (DataRow row in dt.Rows.OfType<DataRow>())
            {
                result.Add(new PersistentObject((int)row.GetInt("objectId")!, (string)row.GetString("serialized")!));
            }
            return result;
        }

        public virtual async Task DeletePersistentObject(string level, int port, int objectId)
        {
            var cmd = GetCommand("DELETE FROM `persistentobjs` WHERE level = @level and port = @port and objectId = @objectId");
            cmd.AddParam("@level", level);
            cmd.AddParam("@port", port);            
            cmd.AddParam("@objectId", objectId);
            await RunNonQuery(cmd);
        }
        
        public async Task<bool> IsCharactersTableEmpty()
        {
            // A cheap way to check if a table is empty, without counting rows
            // If it's empty, it returns 0
            // If it's not empty, it returns 1
            var cmd = GetCommand("SELECT EXISTS(SELECT 1 FROM characters LIMIT 1) as result;");
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return false;
            var result = (int)dt.Rows[0].GetInt("result")!;
            return (result == 0);
        }
    }
}
