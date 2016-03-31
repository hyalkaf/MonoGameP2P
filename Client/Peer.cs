using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    /// <summary>
    /// 
    /// </summary>
    public class Peer : IDisposable
    {

        private static class Request
        {
            public const string TURN = "turn";
            public const string QUIT = "quit";
            public const string RECONNECTED = "reconnected";
            public const string STRIKE = "strike";
            public const string RMPLAYER = "rmplayer";
            public const string ALIVE = "alive";
            public const string TIMEUPDATE = "timeupdate";
            public const string ISLEADER = "isleader";
            public const string GAMEBEGIN = "begin";
        }

        private static class Response
        {
            public const string SUCCESS = "success";
            public const string FAILURE = "failure";
            public const string ERROR = "error";
            public const string UNKNOWN = "unknownrequest";
        }
       // private Timer gameTimer;
        private const int TIMER_START_TIME = 15;
        private int TimerTime = TIMER_START_TIME;
        private PeerInfo myPeerInfo;
        private List<PeerInfo> allPeersInfo;
        private Game.Game game;
        private Object timerLock = new object();
        
        // Initalize variables for peer(client) connecting to other peers(clients)

        private TcpListener _peerListener;

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="playerName"></param>
        /// <param name="peersInfo"></param>
        /// <param name="reconnect"></param>
        public Peer(string playerName, List<PeerInfo> peersInfo , bool reconnect)
        {

            Console.WriteLine("PEER ESTABLISHED For {0}", playerName);
            
            allPeersInfo = peersInfo;

            // Check if peersInfo is populated
            if (allPeersInfo.Count > 0)
            {
                // Get this peerInfo
                myPeerInfo = allPeersInfo.Find(peer => peer.PlayerInfo.Name == playerName);

                string localIP = "";
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                    }
                }
                IPAddress localIpAddr = IPAddress.Parse(localIP);

                _peerListener = new TcpListener(localIpAddr, myPeerInfo.Port);
            }

            InitializeGameState();
            bool synced = false;
            int numOfTries = 3;
            
            if (reconnect)
            {
                while (!synced)
                {
                    myPeerInfo.IsLeader = false;

                    Console.WriteLine("Syncing game state...");
                    int status = SendRequestPeers(Request.RECONNECTED + " " + myPeerInfo.PlayerInfo.PlayerId + " " + myPeerInfo.IPAddr);
                    if (status == 0)
                    {
                        synced = true;
                        break;
                    }
                    numOfTries--;

                    if (numOfTries < 0)
                    {
                        break;
                    }
                    Thread.Sleep(500);
                }

                SendRequestPeers(Request.ISLEADER);
            }
            else
            {

            }
           

            Thread.Sleep(100);
            game.TurnTimer.Change(1000, 1000);
        }

        private void InitializeGameState()
        {
            foreach(PeerInfo pInfo in allPeersInfo)
            {
                Player playerInfo = pInfo.PlayerInfo;
                playerInfo.Turn = playerInfo.PlayerId;
                playerInfo.Position = 0;
                
            }

            game = new Game.Game(allPeersInfo);
            game.TurnTimer = new Timer(TimeCounter);
            Console.WriteLine(game);
            if (myPeerInfo.PlayerInfo.Turn==0)
            {          
                Console.WriteLine("!!You go first! It is your turn now !!");         
            }
            
        }

        public void StartPeerCommunication()
        {

            Thread listenerThread =  new Thread(() => {
                Console.WriteLine("\nDEBUG: Peer listener starts");
                StartListenPeers();
            });
            listenerThread.IsBackground = true;
            listenerThread.Start();

            while (true)
            {
                try { 
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    Console.Write("Enter request (turn, quit): ");
                    string req = Console.ReadLine();
                    req = req.Trim().ToLower();

                    if (NetworkInterface.GetIsNetworkAvailable())
                    {
                        try
                        {
                            if (SendRequestPeers(req) == -1) { Console.WriteLine("INVALID INPUT (turn or quit)"); }
                        }
                        catch (Exception)
                        {
                            listenerThread.Abort();
                            break;
                        }
                    }

                    if (req == Request.QUIT || !allPeersInfo.Contains(myPeerInfo))
                    {
                        listenerThread.Abort();
                        break;
                    }

                    
                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        Console.Write("No network connection! Retry? (Y/N): ");
                        var retry = Console.ReadLine();
                        if (retry.Trim().ToLower() == "N")
                        {
                            listenerThread.Abort();
                            break;
                        }
                    }

                    
                }
                }
                catch (Exception)
                {
                    break;
                }
            }
        }


        private string CurrentStateString()
        {
            string msg = "";
            string peerinfos = "";
            string gamestatus = "";
            foreach (PeerInfo pi in allPeersInfo)
            {
                peerinfos += pi.IPAddr + " " + pi.Port + " " + pi.PlayerInfo.Name + " " + pi.PlayerInfo.PlayerId + " " + pi.Strike + ",";
                gamestatus += pi.PlayerInfo.PlayerId + " " + pi.PlayerInfo.Position + " " + pi.PlayerInfo.Turn + ",";
            }
            msg += peerinfos + "\n" + gamestatus;

            return msg;
        }

        /// <summary>
        /// Establish incoming connections
        /// </summary>
        /// <param name="s"></param>
        /// <param name="id"></param>
        private void EstablishConnection(TcpClient tcpclient)
        {

            TCPMessageHandler msgHandler = new TCPMessageHandler();

            string requestMessage = msgHandler.RecieveMessage(tcpclient);
            Console.WriteLine("DEBUG: Request: " + requestMessage);

            string reqType;
            string reqMsg;
            MessageParser.ParseNext(requestMessage, out reqType, out reqMsg);

            string responseMessage = Response.FAILURE + " " + Response.UNKNOWN;

            // When a peer is broadcasting its turn
            if (reqType == Request.TURN)
            {

                // Parse the request message
                string playerName;
                MessageParser.ParseNext(reqMsg, out playerName, out reqMsg);

                string str_playerId;
                MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                int playerId = int.Parse(str_playerId);

                string diceRolled = reqMsg;
                PeerInfo pi = allPeersInfo.Where(pInfo => pInfo.PlayerInfo.PlayerId == playerId).First();
                Player p = pi.PlayerInfo;

                if (p.Turn == 0)
                {
                    pi.ResetStrike();
                    game.TurnTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    game.MovePlayer(p, int.Parse(diceRolled));
                    Console.WriteLine(game);
                    Console.WriteLine("\nPlayer " + playerId + " (" + playerName + ") move " + diceRolled + " steps.");
                    game.UpdateTurn();

                    responseMessage = Response.SUCCESS + " " + Request.TURN;
                    msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
                    if (myPeerInfo.PlayerInfo.Turn == 0)
                    {
                        Console.WriteLine("\nIt is your turn now :)");

                    }
                    TimerTime = TIMER_START_TIME;
                   
                }
                else
                {
                    responseMessage = Response.ERROR + " " + Request.TURN + " Hey " + playerName + ", it's not your turn yet";
                    msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
                }
                game.TurnTimer.Change(1000, 1000);

                // UPdate IP just in case if IP changes
                if (pi.IPAddr != (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address)
                {
                    pi.IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
                }

            }
            else if (reqType == Request.QUIT)
            {

                responseMessage = Response.SUCCESS + " " + Request.QUIT;
                msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
                // Parse the request message

                // Get PlayerId
                string str_playerId;
                MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                int playerId = int.Parse(str_playerId);

                string turnNum = reqMsg;

                Console.WriteLine("\nPlayer " + playerId + " quit the game! (" + turnNum + ") ");

                //Remove player from the list
                PeerInfo peerToRemove = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == playerId);
                RemovePeerFromGame(peerToRemove);

            } else if (reqType == Request.STRIKE)
            {

                responseMessage = Response.SUCCESS + " " + Request.STRIKE;
                msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
                string str_playerId;
                MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                int playerId = int.Parse(str_playerId);

                StrikePlayer(playerId);
                game.UpdateTurn();
                Console.WriteLine(game);

            } else if (reqType == Request.RECONNECTED)
            {
                lock (timerLock)
                {
                    game.TurnTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    string playerId;

                    MessageParser.ParseNext(reqMsg, out playerId, out reqMsg);
                    PeerInfo reconnectedPeer = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == int.Parse(playerId));
                    reconnectedPeer.ResetStrike();
                    if (reconnectedPeer.IPAddr != (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address)
                    {
                        reconnectedPeer.IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
                    }

                    responseMessage = Response.SUCCESS + " " + Request.RECONNECTED + " " + CurrentStateString();
                    msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
                    Console.WriteLine("(" + playerId + ")" + reconnectedPeer.PlayerInfo.Name + " reconnected!");

                    game.TurnTimer.Change(1000, 1000);
                }

            } else if (reqType == Request.TIMEUPDATE)
            {
                responseMessage = Response.SUCCESS + " " + Request.TIMEUPDATE + " " + TimerTime;
                msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
            } else if (reqType == Request.ALIVE)
            {
                responseMessage = Response.SUCCESS + " " + Request.ALIVE;
                msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
            } else if (reqType == Request.ISLEADER)
            {
                PeerInfo p = allPeersInfo.Find(peer => peer.IsLeader);
                if (p == null)
                {
                    responseMessage = Response.SUCCESS + " " + Request.ISLEADER + " none";
                }
                else {
                    responseMessage = Response.SUCCESS + " " + Request.ISLEADER + " " + p.PlayerInfo.PlayerId;
                }
                msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
            }
            else
            {
                msgHandler.SendResponse(responseMessage + "\n\n", tcpclient);
            }
           

            
        }

        /// <summary>
        /// Send message to all peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        private int SendToAllPeers(string msg)
        {
           // TcpClient[] allPeerTcpClient = new TcpClient[allPeersInfo.Count];
            string[] allResponseMsgs = new string[allPeersInfo.Count];
            string reqType = "";
            Object msgLock = new Object();
            var numOfEmptyResponse = allPeersInfo.Count;

            // Multicast message to all peers
            Parallel.For(0, allPeersInfo.Count, i => {
                // Check if peersInfo is not you and then send info
               PeerInfo aPeer = allPeersInfo[i];
                TcpClient aClient;
                TCPMessageHandler msgHandler = new TCPMessageHandler();
               if (aPeer.PlayerInfo.Name != myPeerInfo.PlayerInfo.Name)
                {
                    bool succPeerConnect = true;
                    int numOfTries = 2;
                    do
                    {
                        aClient = new TcpClient();
                        succPeerConnect = true;
                        try
                        {

                            aClient.ConnectAsync(allPeersInfo[i].IPAddr, aPeer.Port).Wait(3000);
                        }
                        catch (Exception)
                        {
                            Console.Write("Can't connect to peer {0}..  ", aPeer.PlayerInfo.PlayerId);
                            Console.WriteLine("Trying {0} more times... ", numOfTries);
                            Console.WriteLine("TRYING TO SEND " + msg);
                            aClient.Close();
                            succPeerConnect = false;
                            numOfTries--;
                            if (numOfTries == 0)
                            {
                                Console.WriteLine("Unable to communicate with peer " + aPeer.PlayerInfo.PlayerId);
                                Console.WriteLine("Skip it for now...");
                                //SendRequestPeers(Request.STRIKE + " " + aPeer.PlayerInfo.PlayerId);

                                // StrikePlayer(i);
                                //--;
                                return;
                            }
                        }

                    } while (!succPeerConnect && numOfTries > 0);

                    Console.Write("Connected to peer " + aPeer.PlayerInfo.PlayerId + "..  ");

                    Console.Write("Transmitting request to the peer {0} ...", aPeer.PlayerInfo.PlayerId);
                    
                    string responseMessage = msgHandler.SendMessage(msg, aClient);

                    string respType;
                    string respMsg;
                    MessageParser.ParseNext(responseMessage, out respType, out respMsg);
                    numOfEmptyResponse--;
                    if (respType == Response.SUCCESS)
                    {

                        MessageParser.ParseNext(respMsg, out reqType, out respMsg);
                        if (reqType == Request.RECONNECTED)
                        { 
                            allResponseMsgs[i] = respMsg;
      
                        }else if (reqType == Request.ISLEADER)
                        {
                            allResponseMsgs[i] = respMsg;
                        }


                        else
                        {
                            Console.WriteLine(responseMessage);
                        }
                    }
                    else if (respType == Response.FAILURE)
                    {
                        if(respMsg == Response.UNKNOWN)
                        {
                           
                        }
                    }
                    else if (respType == Response.ERROR)
                    {
                        string errType;
                        MessageParser.ParseNext(respMsg, out errType, out respMsg);
                        Console.WriteLine(respMsg);
                        if (errType == Request.TURN)
                        {
                            string errMsg = respMsg;
                            

                        }
                    }

                    aClient.Close();
                }

            });

            
            if (reqType == Request.RECONNECTED)
            {
                string data = "";

                if (allResponseMsgs.Length >= 3)
                {
                  for (int m = 0; m < allResponseMsgs.Length; m++){
                     for (int n = m; n < allResponseMsgs.Length; n++)
                        {   
                            
                            if (allResponseMsgs[m] != allResponseMsgs[n] && allResponseMsgs[m] != null && allResponseMsgs[n] != null)
                            {
                                Console.WriteLine("State unsynced!!");
                                return -1;
                            }
                            else
                            {
                                data = allResponseMsgs[m];
                            }
                 
                        }
                    }
                }
                else if (allResponseMsgs.Length > 1)
                {
                    for (int m = 0; m < allResponseMsgs.Length; m++)
                    {
                        if (allResponseMsgs[m] != null)
                        {
                            data = allResponseMsgs[m];
                            break;
                        }
                    }
                }



                string[] messages = data.Split('\n');
                string strPeerInfos = messages[0];
                string strGameState = messages[1];
                SyncPeersState(strPeerInfos);
                SyncGameState(strGameState);

                Console.WriteLine(game);
 
            }
            else if (reqType == Request.ISLEADER)
            {
                string leaderId = null;
                if (allResponseMsgs.Length >= 3)
                {
                    for (int m = 0; m < allResponseMsgs.Length; m++)
                    {
                        for (int n = m; n < allResponseMsgs.Length; n++)
                        {

                            if (allResponseMsgs[m] != allResponseMsgs[n] && allResponseMsgs[m] != null && allResponseMsgs[n] != null)
                            {
                                return -1;
                            }
                            else
                            {
                                leaderId = allResponseMsgs[m];
                            }

                        }
                    }
                }
                else if (allResponseMsgs.Length > 1)
                {
                    for (int m = 0; m < allResponseMsgs.Length; m++)
                    {
                        if (allResponseMsgs[m] != null)
                        {
                            leaderId = allResponseMsgs[m];
                            break;
                        }
                    }
                }
  
                if(leaderId == null)
                {
                    myPeerInfo.IsLeader = true;
                }
                else
                {
                    allPeersInfo.Find(p => p.PlayerInfo.PlayerId == int.Parse(leaderId)).IsLeader = true;
                }
            }
            



            return 0;

        }

        /// <summary>
        /// Sync game state
        /// </summary>
        /// <param name="strState"></param>
        private void SyncGameState(string strState)
        {
            string[] playerInfos = strState.Split(',');
            foreach (string info in playerInfos)
            {
                string pInfo = info.Trim();
                if (pInfo != String.Empty)
                {
                    string id, pos, turn;
                    MessageParser.ParseNext(pInfo, out id, out pInfo);
                    MessageParser.ParseNext(pInfo, out pos, out turn);
                    Player player = allPeersInfo.Find(p => p.PlayerInfo.PlayerId == int.Parse(id)).PlayerInfo;
                    player.Position = int.Parse(pos);
                    player.Turn = int.Parse(turn);

                    game.UpdatePlayer(player);

                }
            }
        }
        /// <summary>
        /// Sync peer info list 
        /// </summary>
        /// <param name="strState"></param>
        private void SyncPeersState(string strState)
        {
            string[] peerInfos = strState.Split(',');
     
            foreach(string info in peerInfos)
            {
                string pInfo = info.Trim();
                if (pInfo != String.Empty)
                {
                    string ip, port, name, id, strike;
                    MessageParser.ParseNext(pInfo, out ip, out pInfo);
                    MessageParser.ParseNext(pInfo, out port, out pInfo);
                    MessageParser.ParseNext(pInfo, out name, out pInfo);
                    MessageParser.ParseNext(pInfo, out id, out strike);

                    PeerInfo aPeer = allPeersInfo.Find(p => p.PlayerInfo.PlayerId == int.Parse(id) && p.PlayerInfo.Name == name);
                    if(aPeer != myPeerInfo) { 
                        if(aPeer.IPAddr != IPAddress.Parse(ip))
                        {
                            aPeer.IPAddr = IPAddress.Parse(ip);

                        }
                        if(aPeer.Port != int.Parse(port))
                        {
                            aPeer.Port = int.Parse(port);
                        }
                        if(aPeer.Strike != int.Parse(strike))
                        {
                            aPeer.Strike = int.Parse(strike);
                        }

                    }
                }
            }
        }

        /// <summary>
        /// Send/Handle request messages to peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        public int SendRequestPeers(string msg)
        {

            //int playerID = peersInfo.Where(elem => elem.Item3 == playerName).First().Item4;
            // int playerID = myPeerInfo.PlayerInfo.PlayerId;
            string req;
            MessageParser.ParseNext(msg, out req, out msg);
            TCPMessageHandler msgHandler = new TCPMessageHandler();

            if (req == Request.TURN) {
                Random rnd = new Random();
                int dice = rnd.Next(1, 7);
                msg = req +  " " + myPeerInfo.PlayerInfo.Name + " " +
                   myPeerInfo.PlayerInfo.PlayerId + " " + dice;


                game.TurnTimer.Change(Timeout.Infinite, Timeout.Infinite);

                SendToAllPeers(msg);

                if (myPeerInfo.PlayerInfo.Turn == 0) {

                    myPeerInfo.ResetStrike();
                    game.MovePlayer(myPeerInfo.PlayerInfo, dice);

                    Console.WriteLine(game);
                    Console.WriteLine("\nYOU moved " + dice + " steps.");
                    
                    game.UpdateTurn();
                    TimerTime = TIMER_START_TIME;
                }
                
                game.TurnTimer.Change(1000, 1000);



            }
            else if (req == Request.STRIKE)
            {
                SendToAllPeers(req + " " + msg);
            }
            else if (req == Request.QUIT)
            {
                msg = req + " " + myPeerInfo.PlayerInfo.PlayerId + " " + 0;
                SendToAllPeers(msg);

                Dispose();
            }
            else if (req == Request.RECONNECTED)
            {
                
                int status = SendToAllPeers(req + " " + msg);
                return status;
                
            }
           
            else if (req == Request.TIMEUPDATE)
            {
                PeerInfo leader = CurrentLeader;
                TcpClient leaderClient;
                bool succPeerConnect = false;
                int numOfTries = 2;
                do
                {
                    
                    leaderClient = new TcpClient();
                    succPeerConnect = true;
                    try
                    {

                        leaderClient.ConnectAsync(leader.IPAddr, leader.Port).Wait(1000);
                    }
                    catch (Exception)
                    {
                        Console.Write("Can't connect to Leader..  ");
                        Console.WriteLine("Trying {0} more times... ", numOfTries);
                        leaderClient.Close();
                        succPeerConnect = false;
                        numOfTries--;
                        if (numOfTries == 0)
                        {
                            Console.WriteLine("New Leader");
                            leader = CurrentLeader;
                            numOfTries = 2;
                        }
                    }

                } while (!succPeerConnect && numOfTries > 0);

                string responseMessage = msgHandler.SendMessage(req, leaderClient);
                string respStatus;
       
                MessageParser.ParseNext(responseMessage, out respStatus, out responseMessage);

                if (respStatus == Response.SUCCESS)
                {
                    string reqType;
                    string time;
                    MessageParser.ParseNext(responseMessage, out reqType, out time);
                    if (reqType == Request.TIMEUPDATE)
                    {
                        TimerTime = int.Parse(time);
                    }
                }


            }else if(req == Request.ISLEADER)
            {
                SendToAllPeers(req);
            }
            else
            {
                
                if (SendToAllPeers(req + " " + msg) == -1)
                    return -1;
                
            }

            return 0;     
        }
        
        public void TimeCounter(object obj)
        {

            lock (timerLock) { 
                if (IAmLeader)
                {
                    TimerTime--;
                    if (TimerTime < 0)
                    {
                        game.TurnTimer.Change(Timeout.Infinite, Timeout.Infinite);


                        TimerTime = TIMER_START_TIME;


                        int timeOutPlayerId = (allPeersInfo.Find(p => p.PlayerInfo.Turn == 0)).PlayerInfo.PlayerId;

                        SendRequestPeers(Request.STRIKE + " " + timeOutPlayerId);
                        StrikePlayer(timeOutPlayerId);
                        game.UpdateTurn();
                        Console.WriteLine(game);
                        game.TurnTimer.Change(1000, 1000);
                    }
                }
                else
                {
                    SendRequestPeers(Request.TIMEUPDATE);
                    if (TimerTime < 0)
                    {
                        game.TurnTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        TimerTime = TIMER_START_TIME;
                    }
                }


                Console.Write("{0} " , TimerTime);
            }

        }

        /// <summary>
        /// Strike the player if the player is unresponsive
        /// </summary>
        /// <param name="playerId"></param>
        private void StrikePlayer(int playerId)
        {
            PeerInfo playerToBeStriked = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == playerId);

            if (playerToBeStriked.IsStrikeOutOnNextAdd())
            {

                RemovePeerFromGame(playerToBeStriked);
                Console.WriteLine("Player " + playerId + " has been removed due to unresponsiveness.");
            }
            else {

                int strikeNum = playerToBeStriked.AddStrike();

                Console.WriteLine("\nPlayer " + playerToBeStriked + " strike " + strikeNum+"\n");
            }
        }

        private PeerInfo CurrentLeader
        {
            get
            {
                PeerInfo lowestIdPeer = allPeersInfo.Find(peer => peer.IsLeader) ;
                if(lowestIdPeer == null)
                {
                    lowestIdPeer = allPeersInfo.Min();
                }

                bool leaderIsAlive = false;
                TCPMessageHandler msgHandler = new TCPMessageHandler();
                do
                {
                    int counter = 0;

                    do
                    {
                        try
                        {
                            TcpClient testClient = new TcpClient();
                            testClient.ConnectAsync(lowestIdPeer.IPAddr, lowestIdPeer.Port).Wait(3000);
                            byte[] testMsg = new byte[1];
                            msgHandler.SendMessage(Request.ALIVE,testClient);
                            testClient.Close();
                            lowestIdPeer.IsLeader = true;
                            leaderIsAlive = true;
                            break;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Leader is unresponsive...");
                            counter++;
                        }
                    } while (counter < 2);

                    if (!leaderIsAlive)
                    {
                        lowestIdPeer.IsLeader = false;
                        lowestIdPeer = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == ((lowestIdPeer.PlayerInfo.PlayerId+1) % allPeersInfo.Count));
                    }

                } while (!leaderIsAlive);

                return lowestIdPeer;
            }
        }

        private bool IAmLeader
        {
            get
            {
               return CurrentLeader == myPeerInfo;
            }
        }

        private void RemovePeerFromGame(PeerInfo peerToBeRemoved)
        {

            if (peerToBeRemoved == myPeerInfo)
            {
                Console.WriteLine("\n\nYou have been removed from the game!\n");
                Dispose();
                Thread.CurrentThread.Abort();
                return;
            }

            allPeersInfo.Remove(peerToBeRemoved);

            game.RemovePlayer(peerToBeRemoved.PlayerInfo);

            

            Console.WriteLine(game);

            if (IAmLeader)
            {
                TcpClient toServerClient ;
                TCPMessageHandler msgHandler = new TCPMessageHandler();
                toServerClient = new TcpClient();
                toServerClient.Connect(ClientProgram.SERVER_IP, ClientProgram.SERVER_PORT);

                Console.Write("Sending to server: Removing peer {0} from game session...", peerToBeRemoved.PlayerInfo.PlayerId);
               
                string respMsgFromServer = msgHandler.SendMessage(Request.RMPLAYER + " " + peerToBeRemoved.PlayerInfo.Name + " " + peerToBeRemoved.GameSessionId, toServerClient);

                Console.WriteLine("SERVER RESPONSE: " + respMsgFromServer);
            }


        }

        /// <summary>
        /// Start the listener for peer
        /// </summary>
        public void StartListenPeers()
        {
            /* Start Listeneting at the specified port */
            try
            {
                _peerListener.Start();

                Console.WriteLine("The peer is running at port {0}...", (_peerListener.LocalEndpoint as IPEndPoint).Port);
                Console.WriteLine("The local End point is  :" + _peerListener.LocalEndpoint);

                do
                {


                    //Console.WriteLine("Waiting for a connection...");
                    TcpClient tcpclient = _peerListener.AcceptTcpClient();

                    Thread connectionThread = new Thread(() => {
                        EstablishConnection(tcpclient);
                    });
                    connectionThread.IsBackground = true;
                    connectionThread.Start();

                } while (true);

            }
            catch (Exception)
            {
                //Console.WriteLine(e.Message);
                _peerListener.Stop();
                //Console.WriteLine(e.StackTrace);
                Console.WriteLine("You have quit the game!");
            }

        }

        public void Dispose()
        {
            _peerListener.Stop();
            game.TurnTimer.Dispose();
            allPeersInfo = null;
        }

    }
}
