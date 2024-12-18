using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    public class GuildJson
    {
        [JsonProperty]
        public GuildMember[] Members;
        public GuildJson(GuildMember[] members) { Members = members; }
    }
    public class GuildMember
    {
        [JsonProperty]
        public int Id;
        [JsonProperty]
        public string MemberName;
        [JsonProperty]
        public int GuildRank;
        [JsonProperty]
        public bool Online;
        public GuildMember(int id, string name, int guildRank, bool online)
        {
            Id = id;
            MemberName = name;
            GuildRank = guildRank;
            Online = online;            
        }
    }

    public class Guild
    {
        public int Id;
        public string Name;
        readonly Dictionary<int, GuildMember> Members;
        readonly HashSet<Player> OnlineMembers; // it duplicates the information, but it's easy to retreive online members this way without iterating over collections every time

        public Guild(int id, string name)
        {
            Id = id;
            Name = name;
            Members = new();
            OnlineMembers = new();
        }

        public int MembersCount { get => Members.Count; }

        // adds a member when guilds are retrieved from db on startup
        public void PopulateMember(int memberId, string memberName, int guildRank)
        {
            Members.Add(memberId, new GuildMember(memberId, memberName, guildRank, false));
        }

        public HashSet<Player> GetOnlineMembers()
        {
            return OnlineMembers;
        }

        public HashSet<Player> GetOnlineOfficers(int officerRankRequired)
        {
            return OnlineMembers.Where(x => x.GuildRank <= officerRankRequired).ToHashSet();
        }

        public void UpdateMemberRank(int charId, int newRank)
        {
            Members[charId].GuildRank = newRank;
        }

        public Dictionary<int, GuildMember> GetGuildMembers()
        {
            return Members;
        }

        public GuildMember? GetGuildMemberByName(string name)
        {
            foreach (var pair in Members)
            {
                if (pair.Value.MemberName == name) return pair.Value;
            }
            return null;
        }

        public void OnPlayerConnected(Player newPlayer)
        {
            Members[newPlayer.CharId].Online = true;
            OnlineMembers.Add(newPlayer);
        }

        public void OnPlayerDisconnected(Player player)
        {
            Members[player.CharId].Online = false;
            OnlineMembers.Remove(player);
        }

        public void RemoveMember(Player player)
        {
            Members.Remove(player.CharId);
            OnlineMembers.Remove(player);
        }

        // assumes the player isn't online, so we only need to remove from Members
        public void RemoveMemberById(int id)
        {
            Members.Remove(id);
        }

        public GuildMember[] GetAllMembers()
        {
            return Members.Values.ToArray();
        }

        public int GuildMastersCount
        {
            get
            {
                int result = 0;
                foreach (var member in Members)
                {
                    if (member.Value.GuildRank == 0)
                        result++;
                }
                return result;
            }
        }        
    }
}
