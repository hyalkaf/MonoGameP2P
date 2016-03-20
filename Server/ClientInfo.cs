using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ClientInfo
    {
        private string playerName;
        private Socket socket;
        private IPAddress ipaddr;

        public string PlayerName { get { return playerName; } set { playerName = value; } }
        public IPAddress IPAddr {
            get { return ipaddr; }
            set { ipaddr = value; }
        }
    }
}
