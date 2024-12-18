using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;

namespace PersistenceServer
{
    public class DatabaseSqlite : Database
    {
        public DatabaseSqlite(SettingsReader settings) : base(settings)
        {
            ConnectionParams = $"Data Source={settings.SqliteFilename};";
            GetIdentitySqlCommand = "SELECT last_insert_rowid();";
        }

        protected override async Task<DbConnection> GetConnection(string parameters)
        {
            var connection = new SqliteConnection(parameters);
            await connection.OpenAsync();
            return connection;
        }

        protected override DbCommand GetCommand(string parameters, DbConnection? connection) => new SqliteCommand(parameters, (SqliteConnection?)connection);

        public override async Task CheckCreateDatabase(SettingsReader settings)
        {
            // get the number of tables in the database (from a special table called sqlite_master)
            var getTablesQuery = await RunQuery($"SELECT count(*) FROM sqlite_master WHERE type = 'table';");
            // if there's 0 tables, it means the database is new, so we create them
            if (getTablesQuery.GetBigInt(0, "count(*)") == 0) // count returns BigInt
            {
                Console.Write("Database not found or is empty: creating... ");

                // create tables
                await RunNonQuery(
                    @"CREATE TABLE ""accounts"" (
	                    ""id""	INTEGER NOT NULL UNIQUE,
	                    ""name""	TEXT UNIQUE,
                        ""steamid""	TEXT UNIQUE,
	                    ""password""	TEXT,
	                    ""salt""	TEXT,
	                    ""email""	TEXT,
	                    ""status""	INTEGER,
	                    PRIMARY KEY(""id"" AUTOINCREMENT),
	                    UNIQUE(""name"")
                    );
                    CREATE UNIQUE INDEX accname 
                    ON accounts(name);
                    CREATE UNIQUE INDEX accsteamid 
                    ON accounts(steamid);
                    CREATE TABLE ""guilds"" (
	                    ""id""	INTEGER NOT NULL UNIQUE,
	                    ""name""	TEXT UNIQUE,
                        ""serialized""	TEXT,
	                    PRIMARY KEY(""id"" AUTOINCREMENT),
	                    UNIQUE(""name"")
                    );
                    CREATE UNIQUE INDEX guildname 
                    ON guilds(name);
                    CREATE TABLE ""characters"" (
	                    ""id""	INTEGER NOT NULL UNIQUE,
	                    ""name""	TEXT UNIQUE,
                        ""owner""	INTEGER,
	                    ""guild""	INTEGER,
	                    ""guildrank""	INTEGER,
                        ""permissions"" INTEGER NOT NULL DEFAULT 0,
	                    ""serialized""	TEXT,
	                    PRIMARY KEY(""id"" AUTOINCREMENT),
	                    UNIQUE(""name""),
                        FOREIGN KEY(""owner"") REFERENCES ""accounts""(""id"") ON UPDATE CASCADE ON DELETE SET NULL,
                        FOREIGN KEY(""guild"") REFERENCES ""guilds""(""id"") ON UPDATE CASCADE ON DELETE SET NULL
                    );
                    CREATE UNIQUE INDEX charname
                    ON characters(name);
                    CREATE INDEX charowner
                    ON characters(owner);
                    CREATE INDEX charguild
                    ON characters(guild);
                    CREATE TABLE ""servers"" (
                        ""id""  INTEGER NOT NULL UNIQUE,
                        ""port""    INTEGER NOT NULL,
                        ""level""   TEXT NOT NULL,
                        ""serialized""  TEXT,
                        PRIMARY KEY(""id"" AUTOINCREMENT)
                    );
                    CREATE UNIQUE INDEX ServerPortLevel ON servers (port, level);
                    CREATE TABLE ""persistentobjs"" (
                        ""id""  INTEGER NOT NULL UNIQUE,
                        ""level""   TEXT NOT NULL,
                        ""port""    INTEGER NOT NULL,
                        ""objectId""    INTEGER NOT NULL,
                        ""serialized""  TEXT NOT NULL,
                        PRIMARY KEY(""id"" AUTOINCREMENT)
                    );
                    CREATE UNIQUE INDEX PersistentObjIndex ON persistentobjs (objectId, port, level);
                    ");
                // ~create tables

                Console.WriteLine("done.");
            }
            else
            {
                Console.WriteLine($"Database found: {settings.SqliteFilename}");
            }
        }

        public override async Task<int> LoginUser(string accountName, string password)
        {
            var cmd = GetCommand("SELECT id, password, salt, status FROM accounts WHERE name = @accountName");
            cmd.AddParam("@accountName", accountName);
            var dt = await RunQuery(cmd);

            // if not account with this name is found
            if (!dt.HasRows())
            {
                return -1;
            }

            var id = (int)dt.GetBigInt(0, "id")!;
            var status = dt.GetBigInt(0, "status");
            var passwordInDb = dt.GetString(0, "password");
            var salt = dt.GetString(0, "salt");

            // if status is banned
            if (status == -1)
            {
                return -1;
            }

            // if wrong password
            if (passwordInDb != BCrypt.Net.BCrypt.HashPassword(password, salt + Pepper))
            {
                return -1;
            }

            // if everything checks out, allow login by returning user's id
            return id;
        }

        public override async Task<Guild?> CreateGuild(string guildName, int charId)
        {
            // check if guild with this name exists
            var checkCmd = GetCommand("SELECT * FROM guilds WHERE name = @guildName");
            checkCmd.AddParam("@guildName", guildName);
            var dt = await RunQuery(checkCmd);
            if (dt.HasRows()) {
                return null;
            }

            var createGuildCmd = GetCommand("INSERT INTO `guilds` (`id`, `name`) VALUES (NULL, @guildName);");
            createGuildCmd.AddParam("@guildName", guildName);            
            int lastInsertedId = await RunInsert(createGuildCmd);

            var updateCharCmd = GetCommand("UPDATE `characters` SET `guild` = @guildId, `guildRank` = '0' WHERE `characters`.`id` = @charId; ");
            updateCharCmd.AddParam("@guildId", lastInsertedId);
            updateCharCmd.AddParam("@charId", charId);
            await RunNonQuery(updateCharCmd);
            return new Guild(lastInsertedId, guildName);
        }

        /*
         * Due to a bug in Microsoft.Data.Sqlite, DataTable.Load preserves UNIQUE constraints from JOIN queries
         * This makes it impossible to have two rows with the same guild name and it throws an error
         * I submitted a bug report https://github.com/dotnet/efcore/issues/30765
         * In the meantime, we manually create DataTable columns for this query specifically, which allows us to bypass the bug
         */
        public async override Task<Dictionary<int, Guild>> GetGuilds()
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
            await using var conn = await GetConnection(ConnectionParams);

            var cmd = GetCommand(@"
                SELECT guilds.id as guildId, guilds.name as guildName, characters.id as charId, characters.name as charName, characters.guildRank as guildRank FROM guilds
                LEFT JOIN characters
                ON guilds.id = characters.guild
            ", conn);                        
            await using var reader = await cmd.ExecuteReaderAsync();
            DataTable dt = new();
            dt.Columns.Add("guildId", typeof(int));
            dt.Columns.Add("guildName", typeof(string));
            dt.Columns.Add("charId", typeof(int));
            dt.Columns.Add("charName", typeof(string));
            dt.Columns.Add("guildRank", typeof(int));

            dt.Load(reader);
            await cmd.DisposeAsync();

            if (!dt.HasRows()) return result;

            //Console.WriteLine("GuildId, GuildName, CharId, CharName, GuildRank");
            foreach (var row in dt.Rows.OfType<DataRow>())
            {
                var guildId = (int)row.GetInt("guildId")!;
                var guildName = (string)row.GetString("guildName")!;
                var charId = row.GetInt("charId");
                var charName = row.GetString("charName");
                var guildRank = row.GetInt("guildRank");
                //Console.WriteLine($"{guildId}, {guildName}, {charId}, {charName}, {guildRank}");

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

        // Because sqlite and mysql diverge when handling upsert operations (insert or update), we got two different functions
        // The identifier here is level+port
        public override async Task SaveServerInfo(string serializedServerInfo, int port, string level)
        {
            var cmd = GetCommand("INSERT OR REPLACE INTO `servers` (`id`, `port`, `level`, `serialized`) VALUES (NULL, @port, @level, @serialized)");
            cmd.AddParam("@port", port);
            cmd.AddParam("@level", level);
            cmd.AddParam("@serialized", serializedServerInfo);
            await RunNonQuery(cmd);
            Console.WriteLine($"{DateTime.Now:HH:mm} Server Info ({port}-{level}) was saved to DB.");
        }

        // Because sqlite and mysql diverge when handling upsert operations (insert or update), we got two different functions
        // The identifier in the DB is a combination of level+port+objectId
        public override async Task SavePersistentObject(string level, int port, int objectId, string jsonString)
        {
            var cmd = GetCommand("INSERT OR REPLACE INTO `persistentobjs` (`id`, `level`, `port`, `objectId`, `serialized`) VALUES (NULL, @level, @port, @objectId, @serialized)");
            cmd.AddParam("@level", level);
            cmd.AddParam("@port", port);
            cmd.AddParam("@objectId", objectId);
            cmd.AddParam("@serialized", jsonString);
            await RunNonQuery(cmd);
        }
    }
}
