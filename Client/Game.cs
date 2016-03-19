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
        private bool Game_over;                        //is the game over
        private Player Winner;                            //winner of the game

        //construvtor
        public Game(List<PeerInfo> players)
        {
            //initilize board
            for (int i = 0; i < Board.Length; i++)
            {
                Board[i] = new List<Player>();
            }

            //set each player location to 0
            foreach (PeerInfo p in players)
            {
                p.PlayerInfo.Position = 0;
                Board[0].Add(p.PlayerInfo);
            }

            //add all players to first space on board
            //Board[0].Add(players);

            //set maxplayers
            MAX_PLAYERS = players.Count;
        }

        public void move_player(Player current_player, int offset)
        {
            //get current and new locations
            int cur_loc = current_player.Position;
            int new_loc = cur_loc + offset;

            //remove player from board space
            Board[cur_loc].Remove(current_player);
            //update player loction, move and turn
            current_player.Position = new_loc;
            //if turn is 0 set turn to max players
            if (current_player.Turn == 0)
                current_player.Turn = MAX_PLAYERS;
            //otherwise decrement turn
            else
                current_player.Turn -= 1;
            //put current player on new board spac
            Board[new_loc].Add(current_player);
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


    }
}
