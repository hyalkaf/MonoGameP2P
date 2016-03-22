using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class PeerInfo
    {
        private int listeningPort; 
        private Player playerInfo;
        private int strike;
        private int gameSessionId;

        /// <summary>
        ///
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        /// <param name="strike"></param>
        public PeerInfo(string ip, int port, string playername, int playerId, int gameSessionId)
        {
            Initialize(ip, port, new Player(playername, playerId));
            this.gameSessionId = gameSessionId;
        }

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
            this.strike = 0;
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
            return strike+1 > 2;
        }
        public int AddStrike()
        {
            strike++;
            return strike;
        }
        public void ResetStrike()
        {
            strike = 0;
        }
    }
}
