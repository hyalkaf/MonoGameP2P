﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class PeerInfo : IComparable<PeerInfo>
    {

        public Player PlayerInfo { get; set; }

        public IPAddress IPAddr { get; set; }
        public int Port { get; set; }
        public int Strike { set; get; }
        public int GameSessionId{private set; get;}
        public bool IsLeader { get;  set;  }

        public TcpClient SenderClient { get; set; }
        public TcpClient ReceiverClient { get; set; }

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        /// <param name="gameSessionId"></param>
        public PeerInfo(string ip, int port, string playername, int playerId, int gameSessionId)
        {
            Initialize(ip, port, new Player(playername, playerId), gameSessionId);
            
        }

        /// <summary>
        ///  Constructor
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="p"></param>
        /// <param name="gameSessionId"></param>
        public PeerInfo(string ip, int port, Player p, int gameSessionId)
        {
            Initialize(ip,port,p, gameSessionId);
            
        }
        /// <summary>
        /// Initialize IP, port , Player Info , and game session ID to this peer info
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="p"></param>
        /// <param name="gameSessionId"></param>
        private void Initialize(string ip, int port, Player p, int gameSessionId)
        {
            PlayerInfo = p;
            IPAddr = IPAddress.Parse(ip);
            Port = port;
            Strike = 0;
            GameSessionId = gameSessionId;
            if (PlayerInfo.PlayerId == 0)
            {
                IsLeader = true;
            }
            else
            {
                IsLeader = false;
            }
        }

      
        /// <summary>
        /// to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "(" + PlayerInfo.PlayerId + ")" + PlayerInfo.Name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>bool: whether if this peer is max penalty strike</returns>
        public bool IsStrikeOutOnNextAdd()
        {
            return Strike + 1 > 2;
        }

        /// <summary>
        /// Increment and return strike value
        /// </summary>
        /// <returns></returns>
        public int AddStrike()
        {
            Strike++;
            return Strike;
        }
        /// <summary>
        /// Reset strike value to 0
        /// </summary>
        public void ResetStrike()
        {
            Strike = 0;
        }

        /// <summary>
        /// Compare with other peer info by ID number
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PeerInfo other)
        {
            return this.PlayerInfo.PlayerId.CompareTo(other.PlayerInfo.PlayerId);
        }

   
    }
}
