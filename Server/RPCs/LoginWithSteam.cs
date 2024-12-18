using System.Net;
using System;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace PersistenceServer.RPCs
{
    public class LoginWithSteam : BaseRpc
    {
        public LoginWithSteam()
        {
            RpcType = RpcType.RpcLoginSteam; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string steamId = reader.ReadMmoString();
            string authTicket = reader.ReadMmoString();
#if DEBUG
            Console.WriteLine($"(thread {Environment.CurrentManagedThreadId}): Login with steam");
#endif            
            Server!.Processor.ConQ.Enqueue(async () => await ProcessLogin(steamId, authTicket, connection));
        }

        private async Task ProcessLogin(string steamId, string authTicket, UserConnection connection)
        {
            // Querying AuthenticateUserTicket: https://partner.steamgames.com/doc/webapi/ISteamUserAuth            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.BaseAddress = new Uri("https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/");

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(
                    string.Format("?format=json&key={0}&appid={1}&ticket={2}",
                    Server!.Settings.SteamWebApiKey,
                    Server!.Settings.SteamAppId,
                    authTicket
                    ));
            }
            catch (Exception)
            {
                // most likely exception is timeout
                Console.WriteLine("Steam login failed: steam api timeout");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Steam API Timeout"));
                connection.Send(msg);
                return;
            }

            var responseString = await response.Content.ReadAsStringAsync();            
            JObject jObject = JObject.Parse(responseString);

            if (!jObject.ContainsKey("response"))
            {
                Console.WriteLine("Steam login failed: unexpected response");
                Console.WriteLine(responseString);
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Steam API: unexpected response"));
                connection.Send(msg);
                return;
            }

            if (jObject["response"]!["error"] != null)
            {
                Console.WriteLine("Steam login failed: error");
                Console.WriteLine($"Code: {jObject["response"]!["error"]!["errorcode"]}, error: {jObject["response"]!["error"]!["errordesc"]}");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString($"Steam login fail: {jObject["response"]!["error"]!["errordesc"]}"));
                connection.Send(msg);
                return;
            }

            if (jObject["response"]!["params"] == null)
            {
                Console.WriteLine("Steam login failed: unexpected response");
                Console.WriteLine(responseString);
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Steam API: unexpected response"));
                connection.Send(msg);
                return;
            }
            
            var jParam = jObject!["response"]!["params"]!;

            /* We have passed all error checks and now have the following fields:
             
            string jParam["result"] which should always be "OK"
            string jParam["steamid"]
            string jParam["ownersteamid"]            
            bool jParam["vacbanned"]
            bool jParam["publisherbanned"]

            I'm guessing that steamid and ownersteamid may be different if a family member is playing, but doesn't own the game.
            It's up to you how you want to handle it, but for now I'll allow it. If you want to disallow it, uncomment a chunk of code ~25 lines below.

            However, I'll disallow entry if publisherbanned is true.
            */

            if (jParam["result"]!.ToString() != "OK" || jParam["steamid"]!.ToString() != steamId)
            {
                Console.WriteLine($"Steam login failed for provided steamid: {steamId}");
                Console.WriteLine(responseString);
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Steam API: login rejected"));
                connection.Send(msg);
                return;
            }

            if (jParam["publisherbanned"]!.ToObject<bool>())
            {
                Console.WriteLine($"Steam login failed: publisherbanned");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Steam API: you are publisherbanned"));
                connection.Send(msg);
                return;
            }

            /* Uncomment the following chunk if you want to disallow login for family members who don't own the game themselves */
            //if (jParam["steamid"]!.ToString() != jParam["ownersteamid"]!.ToString())
            //{
            //    Console.WriteLine($"Steam login failed: steamid is not a direct owner of the game");
            //    byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Login failed: you must own the game"));
            //    connection.Send(msg);
            //    return;
            //}            

            // retrieves user id from steamid in the database, or creates a steam account if it doesn't exist
            // returns -1 if login failed, due to bad account status            
            int accountId = await Server!.Database.LoginSteamUser(steamId);
            if (accountId >= 0)
            {
                Console.WriteLine($"Steam login successful for Steamid: {steamId}, userid: {accountId}");
                var cookie = BCrypt.Net.BCrypt.GenerateSalt();
                Server!.GameLogic.UserLoggedIn(accountId, cookie, connection);

                // sending true to signify "success", plus a cookie
                // the cookie will allow a reconnection later, when the user changes the level to enter the game server
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(true), WriteMmoString(cookie));
                connection.Send(msg);
            }
            // if account is banned (status == -1)
            else
            {
                Console.WriteLine($"Steam login for '{steamId}' failed: account is banned");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcLoginSteam), ToBytes(false), WriteMmoString("Login failed: temporarily banned"));
                connection.Send(msg);
            }
        }
    }
}