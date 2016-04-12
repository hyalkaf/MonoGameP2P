using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client { 
    public class Peer : IDisposable
    {

        /// <summary>
        /// Main request message type
        /// </summary>
        private static class Request
        {
            /*Request to other peers*/
            public const string CHANGEIP = "changeip";
            public const string QUIT = "quit";
            public const string RECONNECTED = "reconnected";
            public const string STRIKE = "strike";
            public const string TIMEUPDATE = "timeupdate";
            public const string TURN = "turn";
            public const string WHOISLEADER = "whoisleader";
            public const string HANDSHAKE = "handshake";

            /*Request to server*/
            public const string RMPLAYER = "rmplayer";

        }

        /// <summary>
        /// Main response message type
        /// </summary>
        private static class Response
        {
            public const string SUCCESS = "success";
            public const string FAILURE = "failure";
            public const string ERROR = "error";
            public const string UNKNOWN = "unknownrequest";
            public const string NOLEADER = "noleader";
        }

        // All peers information (IP, port, playerID, playerName, etc.)
        private List<PeerInfo> allPeersInfo;
        // My peer information
        private PeerInfo myPeerInfo;
        
        private Game Game;

        // A reset event for stop the loop of listener until connected back to network
        private AutoResetEvent event_HasNetwork = new AutoResetEvent(false);

        // A boolean flag to determine if the user is quitting the game
        private bool quitGame = false;
        private TcpListener _peerListener;
        private Thread listenerThread;

        // Get currentleader
        // Everytime this property is called, the election happens, the most likely case is
        //      current leader is still elected
        private PeerInfo CurrentLeader
        {
            get
            {
                PeerInfo leaderPeer = allPeersInfo.Find(peer => peer.IsLeader);

                // If no leader peer is found, assign peer with lowest id number to be leader
                if (leaderPeer == null) leaderPeer = allPeersInfo.Min();
                do
                {
                    if (ConnectToOnePeer(leaderPeer) == -1)
                        Console.Write(" Leader is unresponsive... ");
                    else
                    {
                        leaderPeer.IsLeader = true;
                        break;
                    }

                    // Current leader is no longer leader
                    leaderPeer.IsLeader = false;

                    // Assign next peer to be leader
                    leaderPeer = allPeersInfo.ElementAt((allPeersInfo.IndexOf(leaderPeer) + 1) % allPeersInfo.Count);

                } while (!leaderPeer.IsLeader);

                return leaderPeer;
            }
        }

        private bool IAmLeader
        {
            get { return CurrentLeader == myPeerInfo; }
        }

        // For interrupting user input
        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        const int VK_RETURN = 0x0D;
        const int WM_KEYDOWN = 0x100;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="playerName"></param>
        /// <param name="peersInfo"></param>
        public Peer(string playerName, List<PeerInfo> peersInfo)
        {
            Console.WriteLine("PEER ESTABLISHED For {0}", playerName);

            // Initialize network change events
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkAvailChangeHandler);
            NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(NetworkAddrChangeHandler);
         
            // Init TCP listener thread
            listenerThread = new Thread(() => { StartTcpListener();} );
            listenerThread.IsBackground = true;

            allPeersInfo = peersInfo;

            // Get this peerInfo
            myPeerInfo = allPeersInfo.Find(peer => peer.PlayerInfo.Name == playerName);
          

            InitializeGameState();

            // Start TCP listener thread
            listenerThread.Start();

            // Establish connection to every peer
            ConnectToEveryone();

            // Ask who is the leader from everyone
            SendTcpRequest(Request.WHOISLEADER);

            // Small pause
            Thread.Sleep(100);

            // Start the game timer
            Game.StartTimer();
        }

        /// <summary>
        ///  This method will send request to all peers for update current state of the game
        /// </summary>
        public void ReconnectBackToGame()
        {
            // peer is reconnecting back to the game, sync with current game state

            bool synced = false;
            int numOfTries = 3;
            do
            {

                myPeerInfo.IsLeader = false;

                Console.WriteLine("Syncing game state...");
                int status = SendTcpRequest(Request.RECONNECTED + " " + myPeerInfo.PlayerInfo.PlayerId + " " + myPeerInfo.IPAddr);
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
            } while (!synced);



        }

        /// <summary>
        /// 
        /// Attempt connection to every peer
        /// 
        /// </summary>
        private void ConnectToEveryone()
        {
            Parallel.ForEach(allPeersInfo, (peer) => {
                ConnectToOnePeer(peer);
            });
             
        }

        /// <summary>
        /// Test if the peer is still connected after serveral attempts if the peer have lost connection
        /// </summary>
        /// <param name="aPeer"></param>
        /// <returns></returns>
        private int TestPeerConnection(PeerInfo aPeer)
        {
            try { 
                if (aPeer.SenderClient != null && aPeer.SenderClient.Client != null && aPeer.SenderClient.Connected)
                {
                    TCPMessageHandler msgHandler = new TCPMessageHandler();
                    msgHandler.SendMessage(Request.HANDSHAKE + " " + myPeerInfo.PlayerInfo.PlayerId, aPeer.SenderClient);
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// Attempt connection to one peer
        /// </summary>
        /// <param name="aPeer"> Peer to connect to </param>
        /// <returns></returns>
        private int ConnectToOnePeer(PeerInfo aPeer)
        {

            // If the peer is already connected, discard the connection
            if (TestPeerConnection(aPeer) == 0) return 0;

            bool succPeerConnect = true;
            int numOfTries = 2;

            // Attempt at most two tries on connecting to peer
            do
            {
                aPeer.SenderClient = new TcpClient();
                succPeerConnect = true;
                try
                {

                    aPeer.SenderClient.ConnectAsync(aPeer.IPAddr, aPeer.Port).Wait(3000);
                                
                }
                catch (Exception)
                {
                    Console.Write("Can't connect to peer {0}..  ", aPeer.PlayerInfo.PlayerId);
                    Console.WriteLine("Trying {0} more times... ", numOfTries);
                    aPeer.SenderClient.Close();
                    succPeerConnect = false;
                    numOfTries--;
                    // Connection failed
                    if (numOfTries == 0)
                    {
                        Console.WriteLine("Unable to reach ({0}){1}", aPeer.PlayerInfo.PlayerId, aPeer.PlayerInfo.Name);
                        Console.WriteLine("Skip it for now...");

                        return -1;
                    }
                }

            } while (!succPeerConnect && numOfTries > 0);

            Console.Write("Connected to peer " + aPeer.PlayerInfo.PlayerId + "..  ");
 
            return 0;
        }

        /// <summary>
        /// Initialize game state
        /// </summary>
        private void InitializeGameState()
        {
            // Set initial status of all players
            Parallel.ForEach(allPeersInfo, (pInfo) =>
            {
                Player playerInfo = pInfo.PlayerInfo;
                playerInfo.Turn = playerInfo.PlayerId;
                playerInfo.Position = 0;

            });

            // Put players on the game board
            Game = new Game(allPeersInfo);
            Game.TurnTimer = new Timer(TimeCounter);

            if (myPeerInfo.PlayerInfo.Turn==0)
            {          
                Console.WriteLine("It is your turn now !! (Type 'turn')");         
            }
            
        }

        /// <summary>
        /// Main user prompt
        /// </summary>
        public void StartPeerCommunication()
        { 
            while (true)
            {
                try {
                    string req = "";
                    // If the user is not quitting, prompt for choices
                    if (!quitGame) { 
                        Console.Write("Enter request (turn, quit): ");
                        req = Console.ReadLine();
                        req = req.Trim().ToLower();
                    }

                    // If the user is quitting and the still connected to network, 
                    //      send proper request to other peers
                    if (!quitGame && NetworkInterface.GetIsNetworkAvailable() && allPeersInfo.Contains(myPeerInfo))
                    {
                        try
                        {
                            if (SendTcpRequest(req) == -1) { Console.WriteLine("INVALID INPUT (turn or quit)"); }
                        }
                        catch (Exception)
                        {
                            listenerThread.Abort();
                            break;
                        }
                    }

                    // User quit the game
                    if (quitGame || !allPeersInfo.Contains(myPeerInfo))
                    {
                        Dispose();
                        Console.Clear();
                        if (Game.Over)
                        {
                            Game.Display();
                            Console.WriteLine("\n\t!! Game Over !!\t");
                        }
                        else { 
                            if(!allPeersInfo.Contains(myPeerInfo)) Console.WriteLine("\n\nYou have been removed from the game!\n");
                            Console.WriteLine("\n\t!! You have quit the game !!\t");
                        }
                        foreach (PeerInfo pi in allPeersInfo)
                        {
                            pi.SenderClient.Close();
                            pi.ReceiverClient.Close();
                            // Remove peer from the list of peers
                            allPeersInfo.Remove(pi);
                        }
                        break;
                    }

                    // Pause the loop if no network connection is detected
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
                catch (Exception)
                {
                    Console.WriteLine("\n\t!! You have quit the game !!\t");
                    break;
                }
            }


        }

        /// <summary>
        /// Convert current state of peer system into string
        /// </summary>
        /// <returns></returns>
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
        /// Establish incoming connections and process request message and reply with corresponding responses
        /// </summary>
        /// <param name="tcpclient"> TcpClient that was accepted by the listener </param>
        private void EstablishAcceptedConnection(TcpClient tcpclient)
        {

            TCPMessageHandler msgHandler = new TCPMessageHandler();

            // Continously process incoming requests until the peer disconnects
            while (true) {
                string requestMessage = "";
                try { 
                  requestMessage = msgHandler.RecieveMessage(tcpclient);
                }
                catch (Exception)
                {
                    return;
                }

                string reqType;
                string reqMsg;
                MessageParser.ParseNext(requestMessage, out reqType, out reqMsg);

                
                // HANDSHAKE request
                if (reqType == Request.HANDSHAKE)
                {
                    int id = int.Parse(reqMsg);

                    allPeersInfo.Find(p=>p.PlayerInfo.PlayerId == id).ReceiverClient = tcpclient;

                    msgHandler.SendResponse(Response.SUCCESS + " " + Request.HANDSHAKE, tcpclient);
                }
                // TURN request
                else if (reqType == Request.TURN)
                {

                    // Parse the request message
                    string playerName;
                    MessageParser.ParseNext(reqMsg, out playerName, out reqMsg);

                    string str_playerId;
                    string diceRolled;
                    MessageParser.ParseNext(reqMsg, out str_playerId, out diceRolled);
                    int playerId = int.Parse(str_playerId);

                    PeerInfo pi = allPeersInfo.Find(pInfo => pInfo.PlayerInfo.PlayerId == playerId);
                    Player p = pi.PlayerInfo;

                    if (p.Turn == 0)
                    {
                        pi.ResetStrike();
                        Game.PauseTimer();
                        Game.MovePlayer(p, int.Parse(diceRolled));

                        Console.WriteLine("\nPlayer " + playerId + " (" + playerName + ") move " + diceRolled + " steps.");
                        Game.UpdateTurn();

                        msgHandler.SendResponse(Response.SUCCESS + " " + Request.TURN, tcpclient);
                        if (myPeerInfo.PlayerInfo.Turn == 0)
                        {
                            Console.WriteLine("\nIt is your turn now :)");

                        }

                        if (Game.Over)
                        {
                            quitGame = true;
                            var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                            PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);

                        }
                        else
                        {
                            Game.ResetTime();
                            Game.StartTimer();
                        }

                    }
                    else
                    {
                        msgHandler.SendResponse(Response.ERROR + " " + Request.TURN + " Hey " + playerName + ", it's not your turn yet", tcpclient);
                    }
                    

                    // Update IP in case of IP changes
                    if (pi.IPAddr != (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address)
                    {
                        pi.IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
                    }

                }
                // QUIT request
                else if (reqType == Request.QUIT)
                {
                    Game.PauseTimer();

                    msgHandler.SendResponse(Response.SUCCESS + " " + Request.QUIT, tcpclient);
            

                    // Get PlayerId
                    string str_playerId;
                    string playerPosition;
                    MessageParser.ParseNext(reqMsg, out str_playerId, out playerPosition);
                    int playerId = int.Parse(str_playerId);

                    string turnNum = reqMsg;

                    Console.WriteLine("\nPlayer " + playerId + " quit the game! (" + turnNum + ") ");

                    //Remove player from the list
                    PeerInfo peerToRemove = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == playerId);
                    RemovePeerFromGame(peerToRemove);
                    Game.StartTimer();
                    break;

                }
                // STRIKE request
                else if (reqType == Request.STRIKE)
                {

                    msgHandler.SendResponse(Response.SUCCESS + " " + Request.STRIKE, tcpclient);
                    string str_playerId;
                    MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                    int playerId = int.Parse(str_playerId);
               
                    StrikePlayer(playerId);

                }
                // RECONNECTED request
                else if (reqType == Request.RECONNECTED)
                {
       
                    Game.PauseTimer();
                    string playerId;

                    MessageParser.ParseNext(reqMsg, out playerId, out reqMsg);
               
                    PeerInfo reconnectedPeer = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == int.Parse(playerId));

                    if (reconnectedPeer != null)
                    {
                        reconnectedPeer.ResetStrike();
                        if (reconnectedPeer.IPAddr != (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address)
                        {
                            Console.WriteLine("RECONNECT NOTICE: {0} changed IP address", reconnectedPeer.PlayerInfo.Name);
                            reconnectedPeer.IPAddr = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;
                        }
                        Console.WriteLine("(" + playerId + ")" + reconnectedPeer.PlayerInfo.Name + " reconnected!");
                    }

                    msgHandler.SendResponse(Response.SUCCESS + " " + Request.RECONNECTED + " " + CurrentStateString(), tcpclient);
               
                    Game.StartTimer();
             

                }
                // TIMEUPDATE request
                else if (reqType == Request.TIMEUPDATE)
                {

                    msgHandler.SendResponse(Response.SUCCESS + " " + Request.TIMEUPDATE + " " + Game.TimerTime, tcpclient);
                }
                // WHOISLEADER request
                else if (reqType == Request.WHOISLEADER)
                {
                    PeerInfo p = allPeersInfo.Find(peer => peer.IsLeader);
                    string leaderResponse = "";
                    if (p == null)
                    {
                        leaderResponse = Response.SUCCESS + " " + Request.WHOISLEADER + " " + Response.NOLEADER;
                    }
                    else {
                        leaderResponse = Response.SUCCESS + " " + Request.WHOISLEADER + " " + p.PlayerInfo.PlayerId;
                    }
                    msgHandler.SendResponse(leaderResponse, tcpclient);
                }
                // CHANGEIP request
                else if(reqType == Request.CHANGEIP)
                {
                    int playerId = int.Parse(reqMsg);
                    PeerInfo peerIpChanged = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == playerId);

                    IPAddress sockIP = (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address;

                    if (peerIpChanged.IPAddr != sockIP)
                    {
                        peerIpChanged.IPAddr = sockIP;
                    }

                    msgHandler.SendResponse(Response.SUCCESS + " " + Request.CHANGEIP + " " + sockIP, tcpclient);
                }
                else
                {
                    msgHandler.SendResponse(Response.FAILURE + " " + Response.UNKNOWN + " Unknown Request (did you have a typo?)", tcpclient);
                }
            }
        }   

        /// <summary>
        /// Sync game state
        /// </summary>
        /// <param name="strState">Parsed message data of the game state</param>
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

                    Game.UpdatePlayer(player);

                }
            }
        }

        /// <summary>
        /// Sync peer info list 
        /// </summary>
        /// <param name="strState">Parsed message data of peer info</param>
        private void SyncPeersState(string strState)
        {
            string[] peerInfos = strState.Split(',');

            List<int> peersSet = new List<int>();

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
                    peersSet.Add(aPeer.PlayerInfo.PlayerId);
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

            foreach (PeerInfo p in allPeersInfo)
            {
                if (!peersSet.Contains(p.PlayerInfo.PlayerId))
                {
                    allPeersInfo.Remove(p);
                }
            }

            if (!allPeersInfo.Exists(p => p.PlayerInfo.Name == myPeerInfo.PlayerInfo.Name))
            {
                quitGame = true;
            }
        }

        /// <summary>
        /// Send/Handle request messages to peers
        /// 
        /// </summary>
        /// <param name="msg">Message to be sent</param>
        public int SendTcpRequest(string msg)
        {

            //int playerID = peersInfo.Where(elem => elem.Item3 == playerName).First().Item4;
            // int playerID = myPeerInfo.PlayerInfo.PlayerId;
            string req;
            MessageParser.ParseNext(msg, out req, out msg);
            TCPMessageHandler msgHandler = new TCPMessageHandler();

            // Send TURN to all peers
            if (req == Request.TURN) {
                int dice = Game.RollDice();
                msg = req +  " " + myPeerInfo.PlayerInfo.Name + " " +
                   myPeerInfo.PlayerInfo.PlayerId + " " + dice;

                SendToEveryone(msg);

                if (myPeerInfo.PlayerInfo.Turn == 0) {
                    Game.PauseTimer();
                    Game.ResetTime();
                    myPeerInfo.ResetStrike();
                    Game.MovePlayer(myPeerInfo.PlayerInfo, dice);

                    Console.WriteLine("\nYOU moved " + dice + " steps.");
                    
                    Game.UpdateTurn();

                    if (Game.Over)
                    {
                        quitGame = true;

                    }else { 

                        
                        Game.StartTimer();
                    }
                }
  
            }
            // Send to all peers which player to strike
            else if (req == Request.STRIKE)
            {
                SendToEveryone(req + " " + msg);
            }
            // Inform all peers that this peer is quitting
            else if (req == Request.QUIT)
            {
                quitGame = true;
                msg = req + " " + myPeerInfo.PlayerInfo.PlayerId + " " + myPeerInfo.PlayerInfo.Position;
                SendToEveryone(msg);
            }
            // Send RECONNECTED request to inform that the peer has just reconnected
            else if (req == Request.RECONNECTED)
            {
                
                int status = SendToEveryone(req + " " + msg);
                return status;
                
            }
            // Inform all peers that this peer has changed IP address
            else if (req == Request.CHANGEIP)
            {
                SendToEveryone(req + " " + msg);
            }      
            // Ask the leader peer for current game time   
            else if (req == Request.TIMEUPDATE)
            {
                PeerInfo leader = CurrentLeader;

                string responseMessage = msgHandler.SendMessage(req, leader.SenderClient);
                string respStatus;
       
                MessageParser.ParseNext(responseMessage, out respStatus, out responseMessage);
           
                string reqType;
                string time;
                MessageParser.ParseNext(responseMessage, out reqType, out time);
                 
                Game.SetTime (int.Parse(time));
                
            }
            // Ask everyone who is the current leader, this is only used at initialization of peer connection
            else if(req == Request.WHOISLEADER)
            {
                SendToEveryone(req);
            }
            else
            {
                if (SendToEveryone(req + " " + msg) == -1)
                    return -1;  
            }

            return 0;     
        }

        /// <summary>
        /// Send message to all peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        private int SendToEveryone(string msg)
        {
            // TcpClient[] allPeerTcpClient = new TcpClient[allPeersInfo.Count];
            string[] allResponseMsgs = new string[allPeersInfo.Count];
            string reqType = "";

            var numOfEmptyResponse = allPeersInfo.Count;

            // Test and connect to everyone before sending message
            ConnectToEveryone();

            // Multicast message to all peers
            Parallel.For(0, allPeersInfo.Count, i => {

                PeerInfo aPeer = allPeersInfo[i];

                TCPMessageHandler msgHandler = new TCPMessageHandler();
                // If the peer to send is not myself and the client is connected, then proceed to send message to the peer
                if (aPeer.PlayerInfo.Name != myPeerInfo.PlayerInfo.Name
                 && aPeer.SenderClient.Client != null && aPeer.SenderClient.Connected)
                {
                    TcpClient aClient = aPeer.SenderClient;

                    Console.Write("Transmitting request to the peer {0} ...", aPeer.PlayerInfo.PlayerId);
                    string responseMessage = "";
                    // In case of disconnection happens while sending, try to reconnect to the peer 
                    do
                    {
                        try
                        {
                            responseMessage = msgHandler.SendMessage(msg, aClient);
                            break;
                        }
                        catch (Exception)
                        {
                            int status = ConnectToOnePeer(aPeer);
                            if (status == -1)
                            {
                                return;
                            }
                        }
                    } while (true);

                    // Process response message from a peer
                    string respType;
                    string respMsg;
                    MessageParser.ParseNext(responseMessage, out respType, out respMsg);
                    numOfEmptyResponse--;
                    // SUCCESS
                    if (respType == Response.SUCCESS)
                    {

                        MessageParser.ParseNext(respMsg, out reqType, out respMsg);
                        // If the request of the response if RECONNECTED or WHOISLEADER
                        //      append the message to array for later comparison to stay in sync
                        if (reqType == Request.RECONNECTED || reqType == Request.WHOISLEADER)
                        {
                            allResponseMsgs[i] = respMsg;

                        }
                        // If this peer IP change, change IP in the peer info
                        else if (reqType == Request.CHANGEIP)
                        {
                            if (myPeerInfo.IPAddr != IPAddress.Parse(respMsg))
                            {
                                myPeerInfo.IPAddr = IPAddress.Parse(respMsg);
                            }
                        }
                    // Any other case just print the message
                        else
                        {
                            Console.WriteLine(responseMessage);
                        }
                    }
                    // FAILURE
                    else if (respType == Response.FAILURE)
                    {
                        Console.WriteLine(respMsg);
                    }
                    // ERROR
                    else if (respType == Response.ERROR)
                    {
                        string errType;
                        MessageParser.ParseNext(respMsg, out errType, out respMsg);
                        Console.WriteLine(respMsg);
                    }
                }

            });

            // Compare all response for reconnection to ensure that the game state is up to sync
            //      and safe to proceed to sync with the given state
            if (reqType == Request.RECONNECTED)
            {
                string data = "";

                // If more than three peers in game, compare rest of the peers
                if (allResponseMsgs.Length >= 3)
                {
                    for (int m = 0; m < allResponseMsgs.Length; m++)
                    {
                        for (int n = m; n < allResponseMsgs.Length; n++)
                        {

                            if (allResponseMsgs[m] != allResponseMsgs[n] && allResponseMsgs[m] != null && allResponseMsgs[n] != null)
                            {
                                Console.WriteLine("State unsynced!!");
                                return -1;
                            }
                            else if(allResponseMsgs[m] != null)
                            {
                                data = allResponseMsgs[m];
                            }else if (allResponseMsgs[n] != null)
                            {
                                data = allResponseMsgs[n];
                            }

                        }
                    }
                }

                // Else just get the state of the other peer
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
                // Worst case, everyone left
                if(data == "")
                {
                    Console.WriteLine("\n\nEverybody disconnected! Game resetted back to beginning...\n");
                    return 0;
                }

                // Proceed to sync and update current state
                string[] messages = data.Split('\n');
                string strPeerInfos = messages[0];
                string strGameState = messages[1];
                SyncPeersState(strPeerInfos);
                SyncGameState(strGameState);

                Game.Display();

            }
            // Compare all peers leader ID and make sure everyone has the same leader
            else if (reqType == Request.WHOISLEADER)
            {
                string leaderId = null;
                // Similar process as above
                if (allResponseMsgs.Length >= 3)
                {
                    for (int m = 0; m < allResponseMsgs.Length; m++)
                    {
                        for (int n = m; n < allResponseMsgs.Length; n++)
                        {
                            if (allResponseMsgs[m] != null && allResponseMsgs[n] != null)
                            {
                                if (allResponseMsgs[m] != allResponseMsgs[n])
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

                // No one responded, set myself to leader
                if (leaderId == null)
                {
                    myPeerInfo.IsLeader = true;
                }
                // No leader assigned, assign leader to peer with lowest ID
                else if (leaderId == Response.NOLEADER)
                {
                    allPeersInfo.Min().IsLeader = true;
                }
                else
                {
                    allPeersInfo.Find(p => p.PlayerInfo.PlayerId == int.Parse(leaderId)).IsLeader = true;
                }
            }


            return 0;

        }

        /// <summary>
        /// 
        /// Callback method for Timer, which normally being executed every second
        /// </summary>
        /// <param name="obj"></param>
        public void TimeCounter(object obj)
        {
            // If this peer is leader, decrement its timer value
            if (IAmLeader)
            {
                Game.SetTime(Game.TimerTime-1);

                // If the timer reaches 0, strike penalty to a player and potentially remove it
                //      send STRIKE message to all peers to inform a penalty strike to the player
                if (Game.TimerTime < 0)
                {
                    Game.PauseTimer();


                    Game.ResetTime();


                    int timeOutPlayerId = (allPeersInfo.Find(p => p.PlayerInfo.Turn == 0)).PlayerInfo.PlayerId;

                    SendTcpRequest(Request.STRIKE + " " + timeOutPlayerId);
                    StrikePlayer(timeOutPlayerId);


                    Game.StartTimer();
                }
            }
            // If the peer is not leader, ask the leader for current time
            else
            {
                SendTcpRequest(Request.TIMEUPDATE);
                if (Game.TimerTime < 0)
                {
                    Game.PauseTimer();
                    Game.ResetTime();
                }
            }

            // Display timer
            Console.Write("{0} " , Game.TimerTime);
            

        }

        /// <summary>
        /// Strike the player if the player is unresponsive
        /// </summary>
        /// <param name="playerId">Player id to be striked</param>
        private void StrikePlayer(int playerId)
        {
            Game.Display();
            Game.UpdateTurn();
            PeerInfo playerToBeStriked = allPeersInfo.Find(peer => peer.PlayerInfo.PlayerId == playerId);

            // if player has been striked 3 times, remove it from the game
            //      else increment strike value
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
        /// <summary>
        /// Remove a peer from the game
        /// </summary>
        /// <param name="peerToBeRemoved"></param>
        private async void RemovePeerFromGame(PeerInfo peerToBeRemoved)
        {
            // Remove peer from the list of peers
            allPeersInfo.Remove(peerToBeRemoved);

            // Remove peer from the game board
            Game.RemovePlayer(peerToBeRemoved.PlayerInfo);

            // If this peer is the one that is being removed, quit the game
            if (peerToBeRemoved.Equals(myPeerInfo))
            {
                // Interrupt any user input
                quitGame = true;
                var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                return;
            }

            // If this peer is leader, inform server to remove player from game session
            if (IAmLeader)
            {
                Console.Write("Sending to server: Removing peer {0} from game session...", peerToBeRemoved.PlayerInfo.PlayerId);
                await Task.Run(()=>SendMessageToServer(Request.RMPLAYER + " " + peerToBeRemoved.PlayerInfo.Name + " " + peerToBeRemoved.GameSessionId));  
            }

            // If winner is declared while winning the game, quit the game
            if (Game.Over)
            {
                quitGame = true;
                var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                return;
            }

        }

        /// <summary>
        /// Send message to server
        /// </summary>
        /// <param name="msg"></param>
        private void SendMessageToServer(string msg)
        {
            TcpClient toServerClient;
            TCPMessageHandler msgHandler = new TCPMessageHandler();

            // Try every 10 second to send message to server if failure occured
            while (true)
            {
                try
                {   
                    toServerClient = new TcpClient();
                    toServerClient.Connect(ClientProgram.SERVER_IP, ClientProgram.SERVER_PORT);

                    string respMsgFromServer = msgHandler.SendMessage(msg, toServerClient);

                    Console.WriteLine("SERVER RESPONSE: " + respMsgFromServer);
                    toServerClient.Close();
                    break;
                }
                catch (Exception)
                {
                    // Pause 10 seconds and resend the message again
                    if (quitGame) break;
                    Console.WriteLine("Server is unresponsive... retrying in 10 seconds...");
                    Thread.Sleep(10000);
                }
            }
    
        }

        /// <summary>
        /// Start the listener for peer
        /// </summary>
        public void StartTcpListener()
        {
            while (!quitGame) {

                // Get local ip and initialize listener
                string localIP = "";
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                    }
                }
              
                _peerListener = new TcpListener(IPAddress.Parse(localIP), myPeerInfo.Port);
                
                /* Start Listeneting at the specified port */
                Console.WriteLine("\nDEBUG: Peer listener starts");
                try
                {
                    // Start listener
                    _peerListener.Start();

                    Console.WriteLine("The peer is running at port {0}...", (_peerListener.LocalEndpoint as IPEndPoint).Port);
                    Console.WriteLine("The local End point is  :" + _peerListener.LocalEndpoint);

                    do
                    {
                        // Wait and accept a TCP connection
                        TcpClient tcpclient = _peerListener.AcceptTcpClient();

                        // Start a connection thread once a client is accepted
                        Thread connectionThread = new Thread(() => {
                            
                            try { 
                            EstablishAcceptedConnection(tcpclient);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("\n\t\tA Connection Left!");
                            }
                        });
                        connectionThread.IsBackground = true;

                        connectionThread.Start();

                    } while (true);

                }
                catch (Exception)
                {
                    _peerListener.Stop();
                    Console.WriteLine("Peer TCP Listener Terminated...");
                    
                }
                if (!NetworkInterface.GetIsNetworkAvailable())
                {   
                    // If netowork is unavailable, pause until network is established again
                    Console.WriteLine("Left the connection...");
                    event_HasNetwork.WaitOne();
                    event_HasNetwork.Reset();
                }
            }

        }
        /// <summary>
        /// Event handler for network availablity status change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NetworkAvailChangeHandler(object sender, EventArgs e)
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                Console.WriteLine("Connected back to network.");
                event_HasNetwork.Set();
                ReconnectBackToGame();
            }
            else
            {
                event_HasNetwork.Reset();
                Console.WriteLine("Make sure you are connected to the network!");
            }
        }
        /// <summary>
        /// Event handler for IP address change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NetworkAddrChangeHandler(object sender, EventArgs e)
        {
            SendTcpRequest(Request.CHANGEIP + " " + myPeerInfo.PlayerInfo.PlayerId);
        }

        /// <summary>
        /// Dispose method for the peer
        /// </summary>
        public void Dispose()
        {
   
            _peerListener.Stop();
            _peerListener.Server.Dispose();
            Game.TurnTimer.Dispose();
            listenerThread.Abort();
            allPeersInfo = null;
        }

    }
}
