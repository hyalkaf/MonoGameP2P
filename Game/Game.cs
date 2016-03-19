//using System;
//using System.Collections;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Client;

//namespace server1
//{
//    class Game
//    {
//        private ArrayList[] Board = new ArrayList[20];    //game board
//        private int MAX_PLAYERS;                              //Max PLayers in game
//        private Boolean Game_over;                        //is the game over
//        private Client.Player Winner;                            //winner of the game

//        //construvtor
//        public Game(ArrayList players)
//        {
//            //initilize board
//            for (int i = 0; i < Board.Length; i++)
//            {
//                Board[i] = new ArrayList();
//            }

//            //set each player location to 0
//            foreach (Player p in players)
//            {
//                p.set_location(0);
//            }

//            //add all players to first space on board
//            Board[0].Add(players);

//            //set maxplayers
//            MAX_PLAYERS = players.Count;
//        }

//        public int roll_dice()
//        {

//            Random roll = new Random();
//            //roll a d6
//            return roll.Next(1, 7);
//        }

//        public void move_player(Player current_player)
//        {
//            //get current and new locations
//            int cur_loc = current_player.get_location();
//            int new_loc = cur_loc + current_player.get_move();

//            //remove player from board space
//            Board[cur_loc].Remove(current_player);
//            //update player loction, move and turn
//            current_player.set_location(new_loc);
//            current_player.set_move(0);
//            //if turn is 0 set turn to max players
//            if (current_player.get_turn() == 0)
//                current_player.set_turn(MAX_PLAYERS);
//            //otherwise decrement turn
//            else
//                current_player.set_turn(current_player.get_turn() - 1);
//            //put current player on new board spac
//            Board[new_loc].Add(current_player);
//        }

//        //clear all players from all spaces, used if update is needed
//        public void clear_board()
//        {
//            for (int i = 0; i < Board.Length; i++)
//            {
//                Board[i].Clear();
//            }
//        }

//        //updated player is added to correct place on board
//        public void update_player(Player p)
//        {
//            Board.[p.get_location()].add(p);
//        }


//    }
//}
