using Microsoft.AspNetCore.Hosting.Server;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PersistenceServer
{
    public class GameLogic
    {
        // When the player first logs in, he gets a connection cookie, and we save him as <cookie, userid>
        // He then selects a character to log in with, and then loads a new level, which causes a disconnect
        // Once he reconnects, he will send his desired character id and a cookie to the game server
        // The game server will pass it along to the persistence server
        // And we'll check if character id exists on the account - if it does, we'll send the character to the game server
        // And the game server will spawn the character for the player
        // And when the player reconnects to the persistence server, he will send exactly the same thing - cookie and character id
        // And we'll log him in, if everything checks out
        // Hence, ConnectionCookies survive disconnects
        readonly Dictionary<string, int> _connectionCookies;
        readonly Dictionary<UserConnection, GameServer> _gameServers;
        // Dictionaries below do not survive reconnects, they have to be repopulated on reconnects
        readonly Dictionary<UserConnection, int> _accountIdByConnection;
        readonly Dictionary<int, UserConnection> _connectionByAccountId;
        readonly Dictionary<UserConnection, int> _charIdByConnection;
        readonly Dictionary<int, UserConnection> _connectionByCharId;
        readonly Dictionary<string, Player> _playersByName;
        readonly Dictionary<UserConnection, Player> _playersByConnection;
        Dictionary<int, Guild> _guildsById; // all guilds, even if players of said guilds didn't come online this session

        public GameLogic()
        {
            _connectionCookies = new();
            _accountIdByConnection = new();
            _connectionByAccountId = new();
            _charIdByConnection = new();
            _connectionByCharId = new();
            _playersByName = new();
            _playersByConnection = new();
            _gameServers = new();
            _guildsById =  new();
        }

        public int GetAccountId(UserConnection conn)
        {
            if(_accountIdByConnection.TryGetValue(conn, out var id))
                return id;
            return -1;
        }

        // Called when the user logs in through the initial menu - he's not in the game world and has no character yet
        public void UserLoggedIn(int accountId, string cookie, UserConnection conn)
        {
            conn.cookie = cookie;
            _connectionCookies.Add(cookie, accountId);
            if (_accountIdByConnection.Remove(conn)) // remove if key exists
            {
                Console.WriteLine("User tried to log in twice, we must never reach here");
                // try to recover
            }
            _accountIdByConnection.Add(conn, accountId);
            // if release, don't allow multiple characters from one account
            // but in debug, it's not unexpected, because we may open two windows in PIE and get two characters from the same account
            // @TODO: maybe we should make a bool allowMultipleCharacters and change ConnectionByAccountId to Dictionary<int, List<UserConnection>>
#if RELEASE
            if (_connectionByAccountId.TryGetValue(accountId, out var oldConn))
            {
                InvalidateCookieForConnection(oldConn);
                DisconnectPlayerFromAllGameServers(oldConn);
                _ = oldConn.Disconnect(); // not awaited
                UserDisconnected(oldConn); // fire this immediately, because otherwise it would fire too late due to threads jumping
            }
#else
            if (!_connectionByAccountId.ContainsKey(accountId))
                _connectionByAccountId.Add(accountId, conn);
#endif
        }

        public int GetAccountIdByCookie(string cookie)
        {
            return _connectionCookies.TryGetValue(cookie, out var connectionValue) ? connectionValue : -1;
        }

        public void UserDisconnected(UserConnection conn)
        {
            if (_gameServers.Remove(conn))
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Game server disconnected");
            }
            if (_charIdByConnection.TryGetValue(conn, out var playerId))
            {
                _connectionByCharId.Remove(playerId);
                _charIdByConnection.Remove(conn);
            }
            if (_accountIdByConnection.TryGetValue(conn, out var userid))
            {
                _connectionByAccountId.Remove(userid);
                _accountIdByConnection.Remove(conn);
            }
            if (_playersByConnection.TryGetValue(conn, out var player))
            {
                var guildId = player.GuildId;
                if (guildId != -1 && _guildsById.TryGetValue(guildId, out var guild)) {
                    guild.OnPlayerDisconnected(player);
                }

                var charname = player.Name;
                Console.WriteLine($"{DateTime.Now:HH:mm} Player disconnected: {charname}");
                _playersByConnection.Remove(conn);
                _playersByName.Remove(charname);
            }            
        }

        // Called when the user connects from the game world - he now has a character
        public void UserReconnected(UserConnection newConn, DatabaseCharacterInfo charInfo)
        {            
            if (_connectionByAccountId.TryGetValue(charInfo.AccountId, out var oldConn))
            {
                // If RELEASE, we don't allow multiple characters from one account
                // But in debug, it's not unexpected, because we may open two windows in PIE and get two characters from the same account
                // @TODO: maybe we should make a bool allowMultipleCharacters and change ConnectionByAccountId to Dictionary<int, List<UserConnection>>
#if RELEASE
                InvalidateCookieForConnection(oldConn);
                DisconnectPlayerFromAllGameServers(oldConn);
                _ = oldConn.Disconnect(); // not awaited
                UserDisconnected(oldConn); // fire this immediately, because otherwise it would fire too late due to threads jumping

                _accountIdByConnection.Add(newConn, charInfo.AccountId);
                _connectionByAccountId.Add(charInfo.AccountId, newConn);
#endif
            }
            else {
                _accountIdByConnection.Add(newConn, charInfo.AccountId);
                _connectionByAccountId.Add(charInfo.AccountId, newConn);
            }

            _connectionByCharId.Add(charInfo.CharId, newConn);
            _charIdByConnection.Add(newConn, charInfo.CharId);
            Player newPlayer = new(newConn, charInfo);
            _playersByConnection.Add(newConn, newPlayer);
            _playersByName.Add(charInfo.Name, newPlayer);
            if (charInfo.Guild != null)
            {
                if (_guildsById.TryGetValue((int)charInfo.Guild, out var guild))
                    guild.OnPlayerConnected(newPlayer);
                else
                    Console.WriteLine("Player connected with a guild id that doesn't exist. This should never happen, look into it.");
            }
        }

        public void ServerConnected(UserConnection conn, GameServer server)
        {
            _gameServers.Add(conn, server);
        }

        public bool IsServer(UserConnection conn)
        {
            return _gameServers.ContainsKey(conn);
        }

        public UserConnection[] GetAllPlayerConnections()
        {
            return _playersByConnection.Keys.ToArray();
        }

        public Player? GetPlayerByName(string name)
        {
            return _playersByName.TryGetValue(name, out var player) ? player : null;
        }

        public Player? GetPlayerByConnection(UserConnection conn)
        {
            return _playersByConnection.TryGetValue(conn, out var player) ? player : null;
        }

        public GameServer? GetServerByConnection(UserConnection conn)
        {
            return _gameServers.TryGetValue(conn, out var server) ? server : null;
        }

        public string GetPlayerName(UserConnection conn)
        {
            return _playersByConnection.TryGetValue(conn, out var player) ? player.Name : "";
        }

        public UserConnection[] GetAllServerConnections()
        {
            return _gameServers.Keys.ToArray();
        }

        public void DisconnectPlayerFromAllGameServers(UserConnection conn)
        {            
            if (_charIdByConnection.TryGetValue(conn, out var oldCharId))
            {
                byte[] msgToServers = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcForceDisconnectPlayer), BaseRpc.ToBytes(oldCharId));
                foreach (var serverConn in GetAllServerConnections())
                {
                    serverConn.Send(msgToServers);
                }
            }
        }

        public void InvalidateCookieForConnection(UserConnection conn)
        {
            _connectionCookies.Remove(conn.cookie);
        }

        public Guild? GetPlayerGuild(UserConnection conn)
        {
            if (_playersByConnection.TryGetValue(conn, out var player))
            {
                var guildId = player.GuildId;
                return _guildsById.TryGetValue(guildId, out var guild) ? guild : null;
            }
            return null;
        }

        public Guild? GetGuildById(int guildId)
        {
            return _guildsById.TryGetValue(guildId, out var guild) ? guild : null;
        }

        public void AssignGuilds(Dictionary<int, Guild> guilds)
        {
            _guildsById = guilds;
        }

        public void CreateGuild(Guild createdGuild, Player guildLeader)
        {
            _guildsById.Add(createdGuild.Id, createdGuild);
            createdGuild.PopulateMember(guildLeader.CharId, guildLeader.Name, 0);
            createdGuild.OnPlayerConnected(guildLeader);
            guildLeader.GuildId = createdGuild.Id;
            guildLeader.GuildRank = 0; // 0 is leader
        }

        public void DeleteGuild(int guildId)
        {
            _guildsById.Remove(guildId);
        }

#pragma warning disable CA1822 // remove the "make it static" warning, AddGuildMember may need to operate on fields later on
        public void AddGuildMember(Guild guild, Player player, int rank)
#pragma warning restore CA1822
        {
            guild.PopulateMember(player.CharId, player.Name, rank);
            guild.OnPlayerConnected(player);
            player.GuildId = guild.Id;
            player.GuildRank = rank;
        }

#pragma warning disable CA1822 // remove the "make it static" warning, DeleteGuildMember may need to operate on fields later on
        // deletion assumes the player isn't online, so we only need to remove from Members
        public void DeleteGuildMember(Guild guild, int charId)
#pragma warning restore CA1822
        {
            guild.RemoveMemberById(charId);
        }

#pragma warning disable CA1822 // remove the "make it static" warning, RemoveGuildMember may need to operate on fields later on
        public void RemoveGuildMember(Guild guild, int charId, Player? player)
#pragma warning restore CA1822
        {
            if (player != null)
            {
                guild.OnPlayerDisconnected(player);
                player.GuildId = -1;
                player.GuildRank = -1;
            }
            guild.RemoveMemberById(charId);
        }
    }
}
