using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    public class GameServer
    {
        public int Port;
        public string Level;
        public GameServer(int port, string level) 
        {
            Port = port;
            Level = level;
        }
    }
}
