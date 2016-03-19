//using System;
//using System.Collections;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Net;
//using System.Net.Sockets;

//namespace server1
//{
//    class peer
//    {
//        private int ID;
//        private String plist;
//        private ArrayList Players = new ArrayList();
//        private Game current_game;
//        private Socket s;
 

        


//        private void update_game()
//        {
//            bool not_updated = true;
//            String list_of_players = null;

//            //loop untill updated
//            while(not_updated)
//            {
//                //loop through possible ID numbers
//                for(int i=1; i< Players.Count;i++)
//                {
//                    //loop through players in game
//                    foreach(Player p in Players)
//                    {
//                        //want to start with lowest id in game and not own id
//                        if(ID != i && p.get_id() == i)
//                        {
//                            //TO DO: correct way of sending request and acceptng message?
//                            //TO DO: instead of nested for loops loop over sockets s
//                            //send update message
//                            ASCIIEncoding asen = new ASCIIEncoding();
//                            byte[] b = asen.GetBytes("update game");
//                            s.Send(b);

//                            //get update
                            
//                            StringBuilder sb = new StringBuilder();
//                            byte[] buffer = new byte[2048];
//                            int bytesRead = s.Receive(buffer);

//                            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

//                            list_of_players = sb.ToString().Trim().ToLower();


//                            if (list_of_players != null)
//                            {
//                                not_updated = true;
//                            }//end if
//                        }//end of if
//                    }//end foreach
//                }//end for
//            }//end while

//            //claer players
//            Players.Clear();

//            //clear board
//            current_game.clear_board();

//            //create new list of players
//            char[] firstsplit = { ',' };
//            char[] secondsplit = { ' ' };
//            String[] temp_player_list = list_of_players.Split(firstsplit);
//            for(int i =0; i< temp_player_list.Length;i++)
//            {
//                String[] temp_player = temp_player_list[i].Split(secondsplit);
//                Player p = new Player(Int32.Parse(temp_player[0]), temp_player[1]);
//                p.set_location(Int32.Parse(temp_player[2]));
//                p.set_move(Int32.Parse(temp_player[3]));
//                p.set_turn(Int32.Parse(temp_player[4]));
//                Players.Add(p);
//            }
           

//            //updat game board
//            foreach (Player p in Players)
//            {
//                current_game.update_player(p);
//            }

//        }//end update_game

//        //generate udate list
//        private void generate_update_list()
//        {

//            plist = "";
//            foreach (Player p in Players)
//            {
//                plist = plist + p.get_id() + " " + p.get_ip() + " " + p.get_location() + " " + p.get_move() + " " + p.get_turn() + " ,";
//            }
//        }

//        /*
//         *send list of players for update

//        if(REQ == "update game")
//        {

//            generate_update_list();
//            ASCIIEncoding asen = new ASCIIEncoding();
//            byte[] b = asen.GetBytes(plist);
//            s.Send(b);    
            
//        }
//        */

//        /* 
//        //idea for connecting to all peers

//        String ownip;
//        int ownid;
//        int ownturn;
//        ArrayList PLayers;

//        //construct
//        public peer(Arraylist Player p)
//        {
//           ...
//           ownip = getip();
//           foreach(Player i in P)
//                Players.add(i);

//           foreach (Player p in PLayers)
//           {
//                if(ownip == p.get_ip())
//                {
//                    ownid = p.get_id();
//                    ownturn = p.get_turn();
//                }
//           }

//           for(int i = 0; i<PLayers.Count; i++)
//           {
//                if(ownid < i+1)
//                    //newthread for listening
//                if(ownid > i+1 )
//                    //new thread for connecting

//            }

//        }
//        */



//    }//end class
//}//end namespace