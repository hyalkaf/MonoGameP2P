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
        private string playerName = null;
        private TcpClient tcpclient;
        private IPAddress ipaddr;

        public ClientInfo(TcpClient tcpclient)
        {
            TcpClient = tcpclient;
            IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
        }

        public string PlayerName {
            get { return playerName; }
            set { playerName = value; }
        }
        public IPAddress IPAddr {
            get { return ipaddr; }
            set { ipaddr = value; }
        }

        public TcpClient TcpClient
        {
            get { return tcpclient; }
            set { tcpclient = value; }
        }
    }
}
