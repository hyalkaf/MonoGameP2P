using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        private PeerInfo myPeerInfo;
        private List<PeerInfo> allPeersInfo;
        private Game.Game game;
        // Initalize variables for peer(client) connecting to other peers(clients)

        private TcpListener _peerListener;
        
        public const string REQ_TURN = "turn";
        public const string REQ_QUIT = "quit";
        public const string REQ_RECONNECTED = "reconnected";
        public const string REQ_STRIKE = "strike";

        public const string RESP_SUCCESS = "success";
        public const string RESP_FAILURE = "failure";
        public const string RESP_ERROR = "error";
        public const string RESP_UNKNOWN = "unknownrequest";
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="playerName"></param>
        /// <param name="peersInfo"></param>
        /// <param name="reconnect"></param>
        public Peer(string playerName, List<PeerInfo> peersInfo , bool reconnect = false)
        {
            
            Console.WriteLine("PEER ESTABLISHED!!");

            allPeersInfo = peersInfo;
            
            Console.WriteLine("For " + playerName);

            // Check if peersInfo is populated
            if (allPeersInfo.Count > 0)
            {
               

                // Get this peerInfo
                // TODO: deal with empty or not existent peer
               
                myPeerInfo = allPeersInfo.Where(peer => peer.PlayerInfo.Name == playerName).First();

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

            if (reconnect)
            {
                SendRequestPeers(REQ_RECONNECTED + " " + myPeerInfo.PlayerInfo.PlayerId + " " + myPeerInfo.IPAddr);
            }
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
            Console.WriteLine(game);
            if (myPeerInfo.PlayerInfo.Turn==0)
            {
                
                Console.WriteLine("!!You go first! It is your turn now !!");
                
            }
        }

        public void StartPeerCommunication()
        {

            new Thread(() => {
                Console.WriteLine("\nDEBUG: Peer listener starts");
                StartListenPeers();
            }).Start();

            while (true)
            {

                Console.Write("Enter request (turn, quit): ");
                string req = Console.ReadLine();
                req = req.Trim().ToLower();

                try
                {
                                    
                    if (SendRequestPeers(req) == -1) { Console.WriteLine("INVALID INPUT (turn or quit)"); }

                    if (req == REQ_QUIT)
                    {
                        break;
                    }
                }
                catch (Exception)
                {
                    break;
                }

            }
        }


        /// <summary>
        /// Establish incoming connections
        /// </summary>
        /// <param name="s"></param>
        /// <param name="id"></param>
        void EstablishConnection(TcpClient tcpclient, int id)
        {

            NetworkStream netStream = tcpclient.GetStream();

            tcpclient.ReceiveBufferSize = 2048;
            byte[] bytes = new byte[tcpclient.ReceiveBufferSize];
           
            netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);


            string requestMessage = Encoding.ASCII.GetString(bytes).Trim();
            requestMessage = requestMessage.Substring(0, requestMessage.IndexOf("\0")).Trim();
            Console.WriteLine("DEBUG: Request: " + requestMessage);

            string responseMessage = RESP_FAILURE + " " + RESP_UNKNOWN;

            // When a peer is broadcasting its turn
            if (requestMessage.StartsWith(REQ_TURN))
            {

                responseMessage = RESP_SUCCESS + " " + REQ_TURN;

                // Parse the request message
                string trimmedMessage = requestMessage.Trim();
                List<char> restOfMessageAfterTurn = trimmedMessage.Substring(REQ_TURN.Length).ToList();


                string playerName = new string(restOfMessageAfterTurn
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray());
                   //.Aggregate((s, ch1) => s + ch1);

                // Get the first number in the turn message
                int playerId = int.Parse(new string(restOfMessageAfterTurn
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .SkipWhile(ch => !char.IsWhiteSpace(ch))
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray()));

                // Get the second the number in the turn message
                int diceRolled = int.Parse(new string(restOfMessageAfterTurn
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .SkipWhile(ch => !char.IsWhiteSpace(ch))
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .SkipWhile(ch => !char.IsWhiteSpace(ch))
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray()));
                // Keep track of peers with their position
                // peersIDToPosition[numberOne] += numberTwo;

                Player p = allPeersInfo.Where(pInfo => pInfo.PlayerInfo.PlayerId == playerId).First().PlayerInfo;
                if (p.Turn == 0)
                {
                    game.move_player(p, diceRolled);
                    Console.WriteLine(game);
                    Console.WriteLine("\nPlayer " + playerId + " (" + playerName + ") move " + diceRolled + " steps.");
                    game.UpdateTurn();

                    if (myPeerInfo.PlayerInfo.Turn == 0)
                    {
                        Console.WriteLine("\n!!It is your turn now :)!!");
                    }
                }
                else
                {
                    responseMessage = RESP_ERROR + " " + REQ_TURN + " Not your turn yet";
                }
                
            }
            else if (requestMessage.StartsWith(REQ_QUIT))
            {

                responseMessage = RESP_SUCCESS + " " + REQ_QUIT;

                // Parse the request message
                string trimmedMessage = requestMessage.Trim();
                List<char> restOfMessageAfterTurn = trimmedMessage.Substring(REQ_QUIT.Length).ToList();

                // Get PlayerId
                int playerId = int.Parse(new string(restOfMessageAfterTurn
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray()));

                // Get the second the number in the turn message
                int turnNum = int.Parse(new string(restOfMessageAfterTurn
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .SkipWhile(ch => !char.IsWhiteSpace(ch))
                   .SkipWhile(ch => char.IsWhiteSpace(ch))
                   .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray()));

                Console.WriteLine("\nPlayer " + playerId + " quit the game! (" + turnNum + ") ");

                //Remove player from the list
                PeerInfo peerToRemove = allPeersInfo.Where(peer => peer.PlayerInfo.PlayerId == playerId).First();
                RemovePeerFromGame(peerToRemove);

            }else if (requestMessage.StartsWith(REQ_STRIKE))
            {

                responseMessage = RESP_SUCCESS + " " + REQ_STRIKE;
                string trimmedMessage = requestMessage.Trim();
                string restOfMessageAfterStrike = trimmedMessage.Substring(REQ_STRIKE.Length);

                int playerId = int.Parse(restOfMessageAfterStrike.Trim());

                StrikePlayer(playerId);
            }


            byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage + "\n\n");
            netStream.Write(byteToSend, 0, byteToSend.Length);
        }

        /// <summary>
        /// Send message to all peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        private void SendToALlPeers(string msg)
        {
            TcpClient[] allPeerTcpClient = new TcpClient[allPeersInfo.Count];
            var responseCounterFlag = 0;
            int playerToBeStriked = -1;
            if (msg.StartsWith(REQ_STRIKE))
            {
                playerToBeStriked = int.Parse(msg.Substring(REQ_STRIKE.Length).Trim());

            }

            // Multicast message to all peers
            Parallel.For(0, allPeerTcpClient.Count(), i => {
                // Check if peersInfo is not you and then send info
                PeerInfo aPeer = allPeersInfo[i];
               

               if (aPeer.PlayerInfo.Name != myPeerInfo.PlayerInfo.Name &&
                aPeer.PlayerInfo.PlayerId != playerToBeStriked)
                {
                    bool succPeerConnect = true;
                    int numOfTries = 2;
                    do
                    {
                        allPeerTcpClient[i] = new TcpClient();
                        allPeerTcpClient[i].SendTimeout = 5000;
                        succPeerConnect = true;
                        try
                        {

                            allPeerTcpClient[i].Connect(allPeersInfo[i].IPAddr, aPeer.Port);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Can't connect to peer " + aPeer.PlayerInfo.PlayerId);
                            Console.WriteLine("Trying... Times to try: " + numOfTries);
                            allPeerTcpClient[i].Close();
                            succPeerConnect = false;
                            numOfTries--;
                            if (numOfTries == 0)
                            {
                                Console.WriteLine("Unable to communicate with peer " + aPeer.PlayerInfo.PlayerId);
                                Console.WriteLine("Skip it for now...");
                                SendRequestPeers(REQ_STRIKE + " " + aPeer.PlayerInfo.PlayerId);

                                StrikePlayer(i);

                                return;
                            }
                        }

                    } while (!succPeerConnect && numOfTries > 0);

                    Console.Write("Connected to peer " + aPeer.PlayerInfo.PlayerId + "..  ");

                    string reqMessage = msg;


                    NetworkStream netStream = allPeerTcpClient[i].GetStream();

                    byte[] bytesToSend = Encoding.ASCII.GetBytes(reqMessage);
                    Console.Write("Transmitting request to the peer {0} ...", aPeer.PlayerInfo.PlayerId);
                    netStream.Write(bytesToSend, 0, bytesToSend.Length);

                    //byte[] buffer = new byte[2048];
                    allPeerTcpClient[i].ReceiveBufferSize = 2048;
                    byte[] bytesRead = new byte[allPeerTcpClient[i].ReceiveBufferSize];

                    //   bytesRead = s.Receive(buffer);
                    netStream.Read(bytesRead, 0, (int)allPeerTcpClient[i].ReceiveBufferSize);
                    Console.WriteLine("... OK!");

                    string responseMessage = Encoding.ASCII.GetString(bytesRead).Trim();
                    responseMessage = responseMessage.Substring(0, responseMessage.IndexOf("\0")).Trim();

                    if (responseMessage.StartsWith(RESP_SUCCESS))
                    {
                        responseCounterFlag++;
                        //Console.WriteLine("NUM OF RESPONSES " + responseCounterFlag);
                    }else if (responseMessage.StartsWith(RESP_FAILURE))
                    {
                        if(responseMessage.Substring(RESP_FAILURE.Length).Trim() == RESP_UNKNOWN)
                        {
                            throw new Exception();
                        }
                    }
                    else if (responseMessage.StartsWith(RESP_ERROR))
                    {
                        string errType = responseMessage.Substring(RESP_ERROR.Length).Trim();

                        if (errType.StartsWith(REQ_TURN))
                        {
                            string errMsg = errType.Substring(REQ_TURN.Length).TrimStart();
                            Console.WriteLine(errMsg);
                        }
                    }

                    allPeerTcpClient[i].Close();
                }

            });
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

            if (msg.StartsWith(REQ_TURN)) {
                Random rnd = new Random();
                int dice = rnd.Next(1, 7);
                msg += " " + myPeerInfo.PlayerInfo.Name + " " +
                   myPeerInfo.PlayerInfo.PlayerId + " " + dice;

                if (myPeerInfo.PlayerInfo.Turn == 0) { 
                    

                    game.move_player(myPeerInfo.PlayerInfo, dice);

                    Console.WriteLine(game);
                    Console.WriteLine("\nYOU moved " + dice + " steps.");
                    
                    game.UpdateTurn();
                }
             
                SendToALlPeers(msg);
            }
            else if (msg.StartsWith(REQ_STRIKE))
            {
                SendToALlPeers(msg);
            }
            else if (msg == REQ_QUIT)
            {
                msg += " " + myPeerInfo.PlayerInfo.PlayerId + " " + 0;
                SendToALlPeers(msg);

                Dispose();
            }
            else if (msg.StartsWith(REQ_RECONNECTED))
            {
                SendToALlPeers(msg);
            }
            else
            {
                try { 
                SendToALlPeers(msg);
                }
                catch (Exception)
                {
                    return -1;
                }
            }

            return 0;     
        }

        public void StartListenPeers()
        {
            /* Start Listeneting at the specified port */
            try { 
                _peerListener.Start();

                Console.WriteLine("The peer is running at port {0}...", (_peerListener.LocalEndpoint as IPEndPoint).Port);
                Console.WriteLine("The local End point is  :" + _peerListener.LocalEndpoint);
                int counter = 0;
                do
                {
                    counter++;
                
                    Console.WriteLine("Waiting for a connection {0} .....", counter);
                    TcpClient tcpclient = _peerListener.AcceptTcpClient();

                    Thread connectionThread = new Thread(() => {
                        EstablishConnection(tcpclient, counter);
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

        private void StrikePlayer(int playerId)
        {
            PeerInfo playerToBeStriked = allPeersInfo.Where(peer => peer.PlayerInfo.PlayerId == playerId).First();

            if (playerToBeStriked.IsStrikeOutOnNextAdd())
            {

                RemovePeerFromGame(playerToBeStriked);
                Console.WriteLine("Player " + playerId + " has been removed due to unresponsiveness.");
            }
            else {

                playerToBeStriked.AddStrike();

                Console.WriteLine("Player " + playerId + " strike " + playerToBeStriked);
            }
        }

        private void RemovePeerFromGame(PeerInfo peerToBeRemoved)
        {
            allPeersInfo.Remove(peerToBeRemoved);

            game.RemovePlayer(peerToBeRemoved.PlayerInfo);
           
            Console.WriteLine(game);
        }

        public void Dispose()
        {
            _peerListener.Stop();

            allPeersInfo = null;
        }

    }
}
