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


        /// <summary>
        /// Uniqie Player Name.
        /// </summary>
        public string PlayerName { get;set; }

        /// <summary>
        /// Player ID.
        /// </summary>
        public int PlayerId{ get;set;}

        /// <summary>
        /// Port number to be used to listen to incoming messages.
        /// </summary>
        public int ListeningPort { get;  set; }

        /// <summary>
        /// IP Address for this client.
        /// </summary>
        public IPAddress IPAddr { get;set; }

        /// <summary>
        /// TCP Client to connect to other players.
        /// </summary>
        public TcpClient TcpClient { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool InQueue { get; set; };

        public ClientInfo()
        {
            PlayerName = null;
            PlayerId = -1;
            ListeningPort = -1;
          }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name=""></param>
        public ClientInfo(IPAddress ip, int port,string playerName, int playerId)
        {
            IPAddr = ip;
            ListeningPort = port;
            PlayerName = playerName;
            PlayerId = playerId;
            TcpClient = null;
            InQueue = false;
        }

        /// <summary>
        /// This method Sets initial information of the client/player -> TCPclient and IP address.
        /// </summary>
        /// <param name="tcpclient">Tcp Client to be used to connect and send infomration for this player.</param>
        public ClientInfo(TcpClient tcpclient)
        {
            TcpClient = tcpclient;
            IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
            InQueue = false;
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
