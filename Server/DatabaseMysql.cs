using MySqlConnector;
using System.Data.Common;
using System.Text;

namespace PersistenceServer
{
    public class DatabaseMysql : Database
    {
        public DatabaseMysql(SettingsReader settings) : base(settings)
        {
            ConnectionParams = $"Server={settings.MysqlHost};" +
                $"Port={settings.MysqlPort};" +
                $"Uid={settings.MysqlUser};" +
                $"Pwd={settings.MysqlPassword};" +
                $"Database={settings.MysqlDatabase}; Allow User Variables=True;";
            GetIdentitySqlCommand = "SELECT @@IDENTITY;";
        }

        protected override async Task<DbConnection> GetConnection(string parameters)
        {
            var connection = new MySqlConnection(parameters);
            await connection.OpenAsync();
            return connection;
        }

        protected override DbCommand GetCommand(string parameters, DbConnection? connection) => new MySqlCommand(parameters, (MySqlConnection?)connection);

        public override async Task CheckCreateDatabase(SettingsReader settings)
        {
            // command that checks if our database exists
            string cmdStr = $"SHOW DATABASES LIKE '{settings.MysqlDatabase}';";
            // special case for connection string: we don't specify the database, because it may not exist yet
            string firstTimeConnectionStr = $"Server={settings.MysqlHost};Port={settings.MysqlPort};Uid={settings.MysqlUser};Pwd={settings.MysqlPassword};";
            var doesDbExistQuery = await RunQuery(cmdStr, firstTimeConnectionStr);
            // if there is no database, create one
            if (!doesDbExistQuery.HasRows())
            {
                Console.Write("Database not found: creating... ");

                // create database
                string collation = settings.MysqlAccentSensitiveCollation ? "utf8mb4_0900_as_ci" : "utf8mb4_0900_ai_ci";
                cmdStr = $"CREATE DATABASE {settings.MysqlDatabase} CHARACTER SET utf8mb4 COLLATE {collation};";
                await RunNonQuery(cmdStr, firstTimeConnectionStr);
                // ~create database

                Console.WriteLine("done.");
            }
            else
            {
                Console.WriteLine("Database found: " + doesDbExistQuery.GetString(0, 0));
            }

            // tables are created if the DB doesn't have them
            await RunNonQuery(
                    @"CREATE TABLE IF NOT EXISTS accounts (
	                    id int NOT NULL AUTO_INCREMENT,
	                    name varchar(50),
                        steamid varchar(20),
	                    password BINARY(60),
	                    salt BINARY(60),
	                    email varchar(255),
	                    status int,
	                    PRIMARY KEY (id),
	                    UNIQUE INDEX NAME (name),
                        UNIQUE INDEX STEAMID (steamid)
                    ) ENGINE = InnoDB;
                    CREATE TABLE IF NOT EXISTS guilds (
	                    id int NOT NULL AUTO_INCREMENT,
	                    name varchar(50) NOT NULL,
                        serialized text,
	                    PRIMARY KEY (id),
	                    UNIQUE INDEX NAME (name)
                    ) ENGINE = InnoDB;
                    CREATE TABLE IF NOT EXISTS characters (
	                    id int NOT NULL AUTO_INCREMENT,
	                    name varchar(50) NOT NULL,
                        owner int,
	                    guild int,
	                    guildrank int,
                        permissions int NOT NULL DEFAULT '0',
	                    serialized text,
	                    PRIMARY KEY (id),
	                    UNIQUE INDEX NAME (name),
                        INDEX OWNER (owner),
                        INDEX GUILD (guild),
                        CONSTRAINT character_owner_fk FOREIGN KEY (owner) REFERENCES accounts(id) ON UPDATE CASCADE ON DELETE SET NULL,
                        CONSTRAINT character_guild_fk FOREIGN KEY (guild) REFERENCES guilds(id) ON UPDATE CASCADE ON DELETE SET NULL
                    ) ENGINE = InnoDB;
                    CREATE TABLE IF NOT EXISTS servers (
                        id int NOT NULL AUTO_INCREMENT,
                        port int NOT NULL,
                        level text NOT NULL,
                        serialized text,
                        PRIMARY KEY (id),
                        UNIQUE INDEX PORT_LEVEL (`port`, `level`(100))
                    ) ENGINE = InnoDB;
                    CREATE TABLE IF NOT EXISTS persistentobjs (
                        id int NOT NULL AUTO_INCREMENT,
                        level text NOT NULL,
                        port int NOT NULL,
                        objectId int NOT NULL,
                        serialized text NOT NULL,
                        PRIMARY KEY (id),
                        UNIQUE INDEX PERSISTENT_OBJ_INDEX (`objectId`, `port`, `level`(100))
                    ) ENGINE = InnoDB;"
                );
            // ~create tables            
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

            var id = (int)dt.GetInt(0, "id")!;
            var status = dt.GetInt(0, "status")!;
            var passwordInDb = Encoding.UTF8.GetString(dt.GetBinaryArray(0, "password"));
            var salt = Encoding.UTF8.GetString(dt.GetBinaryArray(0, "salt"));

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
            // if the name is taken, insert will fail silently, returning 0 inserted rows
            var cmd = GetCommand("INSERT IGNORE INTO `guilds` (`id`, `name`) VALUES (NULL, @guildName);");
            cmd.AddParam("@guildName", guildName);
            int lastInsertedId = await RunInsert(cmd);
            if (lastInsertedId == 0)
            {
                return null;
            }
            var cmd2 = GetCommand("UPDATE `characters` SET `guild` = @guildId, `guildRank` = '0' WHERE `characters`.`id` = @charId; ");
            cmd2.AddParam("@guildId", lastInsertedId);
            cmd2.AddParam("@charId", charId);
            await RunNonQuery(cmd2);
            return new Guild(lastInsertedId, guildName);
        }

        // Because sqlite and mysql diverge when handling upsert operations (insert or update), we got two different functions
        // The identifier here is level+port
        public override async Task SaveServerInfo(string serializedServerInfo, int port, string level)
        {
            var cmd = GetCommand("INSERT INTO `servers` (`id`, `port`, `level`, `serialized`) VALUES (NULL, @port, @level, @serialized) ON DUPLICATE KEY UPDATE serialized = VALUES(serialized);");
            cmd.AddParam("@port", port);
            cmd.AddParam("@level", level);
            cmd.AddParam("@serialized", serializedServerInfo);
            await RunInsert(cmd);
            Console.WriteLine($"{DateTime.Now:HH:mm} Server Info ({port}-{level}) was saved to DB.");
        }

        // Because sqlite and mysql diverge when handling upsert operations (insert or update), we got two different functions
        // The identifier in the DB is a combination of level+port+objectId
        public override async Task SavePersistentObject(string level, int port, int objectId, string jsonString)
        {
            var cmd = GetCommand("INSERT INTO `persistentobjs` (`id`, `level`, `port`, `objectId`, `serialized`) VALUES (NULL, @level, @port, @objectId, @serialized) ON DUPLICATE KEY UPDATE serialized = VALUES(serialized);");
            cmd.AddParam("@level", level); 
            cmd.AddParam("@port", port);
            cmd.AddParam("@objectId", objectId);
            cmd.AddParam("@serialized", jsonString);
            await RunInsert(cmd);
        }
    }
}