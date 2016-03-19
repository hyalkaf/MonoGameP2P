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
        private IPAddress ipaddr;
        private int listeningPort; 
        private Player playerInfo;
        private int strike;

        /// <summary>
        ///
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        /// <param name="strike"></param>
        public PeerInfo(string ip, int port, string playername, int playerId, int strike)
        {
            Initialize(ip, port, new Player(playername, playerId), strike);
        }

        public PeerInfo(string ip, int port, Player p, int strike)
        {
            Initialize(ip,port,p,strike);
        }
        private void Initialize(string ip, int port, Player p, int strike)
        {
            playerInfo = p;
            ipaddr = IPAddress.Parse(ip);
            listeningPort = port;
            this.strike = strike;
        }

        public Player PlayerInfo
        {
            get { return playerInfo; }
            set { playerInfo = value; }
        }

        public IPAddress IPAddr
        {
            get { return ipaddr; }
            set { ipaddr = value; }
        }
        public int Port {
            get { return listeningPort; }
            set { listeningPort = value; }
        }

        public bool IsStrikeOutOnNextAdd()
        {
            return strike+1 > 2;
        }
        public void AddStrike()
        {
            strike++;
        }
        public void ResetStrike()
        {
            strike = 0;
        }
    }
}
