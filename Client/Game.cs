using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client;
using System.Collections.Generic;

namespace Game
{
    public class Game
    {
        private List<Player>[] Board = new List<Player> [20];    //game board
        private int MAX_PLAYERS;                              //Max PLayers in game
        private Player winner;                            //winner of the game

        //construvtor
        public Game(List<PeerInfo> players)
        {
            //initilize board
            for (int i = 0; i < Board.Length; i++)
            {
                Board[i] = new List<Player>();
            }

            //set each player location to 0
            // and add all players to first space on board
            foreach (PeerInfo p in players)
            {
                Player player = p.PlayerInfo;
                Board[0].Add(player);
            }

            //set maxplayers
            MAX_PLAYERS = players.Count;

            winner = null;
        }

        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="current_player"></param>
        /// <param name="offset"></param>
        public void move_player(Player current_player, int offset)
        {
            //get current and new locations
            int cur_loc = current_player.Position;
            int new_loc = cur_loc + offset;
            if (new_loc >= Board.Length - 1 )
            {
                new_loc = Board.Length - 1;
                Winner = current_player;
            }                    
           
           
            //remove player from board space
            Board[cur_loc].Remove(current_player);
            //update player loction, move and turn
            current_player.Position = new_loc;
            //if turn is 0 set turn to max players
            //if (current_player.Turn == 0)
            //    current_player.Turn = MAX_PLAYERS-1;
            ////otherwise decrement turn
            //else
            //    current_player.Turn -= 1;
            //put current player on new board spac
            Board[new_loc].Add(current_player);

        }

        public void UpdateTurn()
        {
            foreach (List<Player> players in Board)
            {
                foreach (Player p in players)
                {
                    if (p.Turn == 0)
                        p.Turn = MAX_PLAYERS - 1;
                    //otherwise decrement turn
                    else
                        p.Turn -= 1;
                }

            }
        }

        //clear all players from all spaces, used if update is needed
        public void clear_board()
        {
            for (int i = 0; i < Board.Length; i++)
            {
                Board[i].Clear();
            }
        }

        //updated player is added to correct place on board
        public void update_player(Player p)
        {
            Board[p.Position].Add(p);
        }

        public int MaxPlayers
        {
            get { return MaxPlayers; }
            set { MAX_PLAYERS = value; }
        }

        public void RemovePlayer(Player pToBeRemoved)
        {
            int removedPlayerTurnNum = pToBeRemoved.Turn;

            Board[pToBeRemoved.Position].Remove(pToBeRemoved);

            foreach (List<Player> players in Board)
            {
                foreach (Player p in players)
                {
                    if (p.Turn > removedPlayerTurnNum)
                    {
                        p.Turn -= 1;
                    }

                }
            }
            MAX_PLAYERS--;
        }

        public override string ToString()
        {
            string display = "\n-------------------------------\n";

            foreach(List<Player> players in Board)
            {
                display += "["+(Array.IndexOf(Board,players)+1)+"] ";
                foreach(Player p in players)
                {
                    display += "(" + p.PlayerId + ")" + p.Name + " ";
                }
                display += "\n";
            }
            display += "-------------------------------\n";
            return display;
        }

        public bool Over
        {
            get { return Winner != null;  }
        }

        public Player Winner
        {
            get { return winner; }
            internal set {
                winner = value;
                Console.WriteLine("\n---------------------------------");
                Console.WriteLine("The Winner is (" + winner.PlayerId+")"+winner.Name);
                Console.WriteLine("---------------------------------\n");
            }
        }

    }
}
