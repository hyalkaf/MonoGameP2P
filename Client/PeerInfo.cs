using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class PeerInfo : IComparable<PeerInfo>
    {
        private int listeningPort; 
        private Player playerInfo;
        private int gameSessionId;
        public bool IsLeader
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        /// <param name="gameSessionId"></param>
        public PeerInfo(string ip, int port, string playername, int playerId, int gameSessionId)
        {
            Initialize(ip, port, new Player(playername, playerId));
            this.gameSessionId = gameSessionId;
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="p"></param>
        /// <param name="gameSessionId"></param>
        public PeerInfo(string ip, int port, Player p, int gameSessionId)
        {
            Initialize(ip,port,p);
            this.gameSessionId = gameSessionId;
        }
        private void Initialize(string ip, int port, Player p)
        {
            playerInfo = p;
            IPAddr = IPAddress.Parse(ip);
            listeningPort = port;
            Strike = 0;
            if (PlayerInfo.PlayerId == 0)
            {
                IsLeader = true;
            }
            else
            {
                IsLeader = false;
            }
        }

        public Player PlayerInfo
        {
            get { return playerInfo; }
            set { playerInfo = value; }
        }

        public IPAddress IPAddr
        {
            get;
            set;
        }

        public override string ToString()
        {
            return "(" + PlayerInfo.PlayerId + ")" + PlayerInfo.Name;
        }

        public int Port {
            get { return listeningPort; }
            set { listeningPort = value; }
        }

        public int GameSessionId
        {
            get { return gameSessionId; }
        }


        public bool IsStrikeOutOnNextAdd()
        {
            return Strike + 1 > 2;
        }
        public int AddStrike()
        {
            Strike++;
            return Strike;
        }
        public void ResetStrike()
        {
            Strike = 0;
        }

        public int CompareTo(PeerInfo other)
        {
            return this.PlayerInfo.PlayerId.CompareTo(other.PlayerInfo.PlayerId);
        }

        public int Strike
        {
            internal set;
            get;
            
        }
    }
}
