using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Client
{
    public class Player
    {
        private string name;
        private int id = -1;
        private int inGamePosition = -1;
        private int inGameTurn = -1;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        public Player(string playername, int playerId)
        {
            name = playername;
            id = playerId;
        }

   
        public int PlayerId
        {
            get { return id; }
            set { id = value; }
        }

        public int Position {
            get { return inGamePosition; }
            set { inGamePosition = value; }
        }

        public string Name {
            get { return name; }
            set { name = value; }
        }

        public int Turn
        {
            get { return inGameTurn; }
            set { inGameTurn = value; }
        }

    }
}
