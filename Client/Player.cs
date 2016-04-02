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

        public int PlayerId { get; set; }
        public string Name { get; set; }
        public int Position { get; set; }

        public int Turn
        {
            get { return inGameTurn; }
            set
            {
                inGameTurn = value;
                Console.Write("Until " + value + " is " + Name + "'s turn. ");
            }
        }

        private int inGameTurn = -1;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        public Player(string playername, int playerId)
        {
            Name = playername;
            PlayerId = playerId;
        }

        public Player(string playername)
        {
            Name = playername;
            PlayerId = -1;
        }

        public void IsItMyTurn()
        {
            if (Turn == 0)
            {
                Console.Write("\n\tIt is Your Turn Now!");
            }
        }
       

    }
}
