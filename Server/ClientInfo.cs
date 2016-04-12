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
        /// Flag for identifyng clients who are still waiting to be matched.
        /// </summary>
        public bool InQueue { get; set; }

        /// <summary>
        /// This constructor creates a client info object with 
        /// null and -1 values for properties.
        /// </summary>
        public ClientInfo()
        {
            PlayerName = null;
            PlayerId = -1;
            ListeningPort = -1;
          }

        /// <summary>
        /// This constructor creates a client info object with 
        /// the passed in info.
        /// </summary>
        /// <param name="ip">IP address for this client</param>
        /// <param name="port">Port number this client is listening on</param>
        /// <param name="playerName">Player name that is unique to this player in the whole game universe of all game sessions.</param>
        /// <param name="playerId">Player ID that is unique to the player in a specific game session.</param>
        /// <param name="inQueue">Flag to identify whether this client is waiting for a game. False by default.</param>
        public ClientInfo(IPAddress ip, int port,string playerName, int playerId, bool inQueue = false)
        {
            IPAddr = ip;
            ListeningPort = port;
            PlayerName = playerName;
            PlayerId = playerId;
            // Tcp Client is null at the creation of the object.
            // It will be mutated later on.
            TcpClient = null;
            this.InQueue = inQueue;
        }

        /// <summary>
        /// This method Sets initial information of the client/player -> TCPclient and IP address.
        /// Here Tcp Client can't be null.
        /// </summary>
        /// <param name="tcpclient">Tcp Client to be used to connect and send infomration for this player.</param>
        public ClientInfo(TcpClient tcpclient)
        {
            TcpClient = tcpclient;
            IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
            InQueue = false;
        }


        
        /// <summary>
        /// Convert client info to a string message delimited by spaces in between each piece of information without inQueue info
        /// </summary>
        /// <returns>This client Info in a string delimited by spaces.</returns>
        public string ToMessageForGameSession()
        {
            return IPAddr + " " + ListeningPort + " " + PlayerName + " " + PlayerId;
        }

        /// <summary>
        /// Convert client info to a string message delimited by spaces in between each piece of information with inQueue info
        /// </summary>
        /// <returns>This client Info in a string delimited by spaces.</returns>
        public string ToMessageForGameQueue()
        {
            return IPAddr + " " + ListeningPort + " " + PlayerName + " " + PlayerId + " " + (InQueue.Equals(0) ? 0 : 1);
        }
    }
}
