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

        // Turn number
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
        /// Constructor with name and id
        /// </summary>
        /// <param name="playername"></param>
        /// <param name="playerId"></param>
        public Player(string playername, int playerId)
        {
            Name = playername;
            PlayerId = playerId;
        }

        /// <summary>
        /// Display if it is this player's turn 
        /// </summary>
        public void IsItMyTurn()
        {
            if (Turn == 0)
            {
                Console.Write("\n\tIt is Your Turn Now!");
            }
        }
       

    }
}
