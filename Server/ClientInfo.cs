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
        private int playerId = -1;
        private int listeningPort = -1;

        private TcpClient tcpclient;
        private IPAddress ipaddr;

        /// <summary>
        /// This method Sets initial information of the client/player -> TCPclient and IP address.
        /// </summary>
        /// <param name="tcpclient">Tcp Client to be used to connect and send infomration for this player.</param>
        public ClientInfo(TcpClient tcpclient)
        {
            TcpClient = tcpclient;
            IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
        }


        /// <summary>
        /// Uniqie Player Name.
        /// </summary>
        public string PlayerName {
            get { return playerName; }
            set { playerName = value; }
        }

        /// <summary>
        /// Player ID.
        /// </summary>
        public int PlayerId
        {
            get { return playerId; }
            set { playerId = value; }
        }

        /// <summary>
        /// Port number to be used to listen to incoming messages.
        /// </summary>
        public int ListeningPort
        {
            get { return listeningPort; }
            set { listeningPort = value; }
        }

        /// <summary>
        /// IP Address for this client.
        /// </summary>
        public IPAddress IPAddr {
            get { return ipaddr; }
            set { ipaddr = value; }
        }

        /// <summary>
        /// TCP Client to connect to other players.
        /// </summary>
        public TcpClient TcpClient
        {
            get { return tcpclient; }
            set { tcpclient = value; }
        }

        /// <summary>
        /// Convert client info to a string message delimited by spaces in between each piece of information
        /// </summary>
        /// <returns>This client Info in a string delimited by spaces.</returns>
        public string ToMessage()
        {
            return IPAddr + " " + ListeningPort + " " + PlayerName + " " + PlayerId;
        }
    }
}
