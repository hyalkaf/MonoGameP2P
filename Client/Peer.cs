﻿using System;
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

        private static class Request
        {
            public const string TURN = "turn";
            public const string QUIT = "quit";
            public const string RECONNECTED = "reconnected";
            public const string STRIKE = "strike";
        }

        private static class Response
        {
            public const string SUCCESS = "success";
            public const string FAILURE = "failure";
            public const string ERROR = "error";
            public const string UNKNOWN = "unknownrequest";
        }

        private PeerInfo myPeerInfo;
        private List<PeerInfo> allPeersInfo;
        private Game.Game game;
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
                SendRequestPeers(Request.RECONNECTED + " " + myPeerInfo.PlayerInfo.PlayerId + " " + myPeerInfo.IPAddr);
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

                    if (req == Request.QUIT)
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
        void EstablishConnection(TcpClient tcpclient)
        {

            NetworkStream netStream = tcpclient.GetStream();

            tcpclient.ReceiveBufferSize = 2048;
            byte[] bytes = new byte[tcpclient.ReceiveBufferSize];
           
            netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);


            string requestMessage = Encoding.ASCII.GetString(bytes).Trim();
            requestMessage = requestMessage.Substring(0, requestMessage.IndexOf("\0")).Trim();
            Console.WriteLine("DEBUG: Request: " + requestMessage);

            string reqType;
            string reqMsg;
            MessageParser.ParseNext(requestMessage,out reqType,out reqMsg);
         
            string responseMessage = Response.FAILURE + " " + Response.UNKNOWN;

            // When a peer is broadcasting its turn
            if (reqType == Request.TURN)
            {

                responseMessage = Response.SUCCESS + " " + Request.TURN;

                // Parse the request message
                string playerName;
                MessageParser.ParseNext(reqMsg, out playerName, out reqMsg);

                string str_playerId;
                MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                int playerId = int.Parse(str_playerId);

                string diceRolled = reqMsg;

                Player p = allPeersInfo.Where(pInfo => pInfo.PlayerInfo.PlayerId == playerId).First().PlayerInfo;


                if (p.Turn == 0)
                {
                    game.move_player(p, int.Parse(diceRolled));
                    Console.WriteLine(game);
                    Console.WriteLine("\nPlayer " + playerId + " (" + playerName + ") move " + diceRolled + " steps.");
                    game.UpdateTurn();

                    if (myPeerInfo.PlayerInfo.Turn == 0)
                    {
                        Console.WriteLine("\nIt is your turn now :)");
                    }
                }
                else
                {
                    responseMessage = Response.ERROR + " " + Request.TURN + " Hey " + playerName + ", it's not your turn yet";
                }
                
            }
            else if (reqType == Request.QUIT)
            {

                responseMessage = Response.SUCCESS + " " + Request.QUIT;

                // Parse the request message

                // Get PlayerId
                string str_playerId;
                MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                int playerId = int.Parse(str_playerId);

                string turnNum = reqMsg;

                Console.WriteLine("\nPlayer " + playerId + " quit the game! (" + turnNum + ") ");

                //Remove player from the list
                PeerInfo peerToRemove = allPeersInfo.Where(peer => peer.PlayerInfo.PlayerId == playerId).First();
                RemovePeerFromGame(peerToRemove);

            }else if (reqType == Request.STRIKE)
            {

                responseMessage = Response.SUCCESS + " " + Request.STRIKE;

                string str_playerId;
                MessageParser.ParseNext(reqMsg, out str_playerId, out reqMsg);
                int playerId = int.Parse(str_playerId);

                StrikePlayer(playerId);
            }else if (reqType == Request.RECONNECTED)
            {

            }


            byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage + "\n\n");
            netStream.Write(byteToSend, 0, byteToSend.Length);
        }

        /// <summary>
        /// Send message to all peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        private void SendToAllPeers(string msg)
        {
            TcpClient[] allPeerTcpClient = new TcpClient[allPeersInfo.Count];
            var responseCounterFlag = 0;
            int playerToBeStriked = -1;
            if (msg.StartsWith(Request.STRIKE))
            {
                playerToBeStriked = int.Parse(msg.Substring(Request.STRIKE.Length).Trim());

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
                                SendRequestPeers(Request.STRIKE + " " + aPeer.PlayerInfo.PlayerId);

                                StrikePlayer(i);

                                return;
                            }
                        }

                    } while (!succPeerConnect && numOfTries > 0);

                    Console.Write("Connected to peer " + aPeer.PlayerInfo.PlayerId + "..  ");

                    NetworkStream netStream = allPeerTcpClient[i].GetStream();

                    byte[] bytesToSend = Encoding.ASCII.GetBytes(msg);
                    Console.Write("Transmitting request to the peer {0} ...", aPeer.PlayerInfo.PlayerId);
                    netStream.Write(bytesToSend, 0, bytesToSend.Length);

                    //byte[] buffer = new byte[2048];
                    allPeerTcpClient[i].ReceiveBufferSize = 2048;
                    byte[] bytesRead = new byte[allPeerTcpClient[i].ReceiveBufferSize];

                    //   bytesRead = s.Receive(buffer);
                    netStream.Read(bytesRead, 0, (int)allPeerTcpClient[i].ReceiveBufferSize);
                    Console.WriteLine("... OK!");

                    string responseMessage = Encoding.ASCII.GetString(bytesRead);
                    responseMessage = responseMessage.Substring(0, responseMessage.IndexOf("\0")).Trim();

                    string respType;
                    string respMsg;
                    MessageParser.ParseNext(responseMessage, out respType, out respMsg);

                    if (respType == Response.SUCCESS)
                    {
                        responseCounterFlag++;
                        Console.WriteLine(responseMessage);
                    }
                    else if (respType == Response.FAILURE)
                    {
                        if(respMsg == Response.UNKNOWN)
                        {
                            throw new Exception();
                        }
                    }
                    else if (respType == Response.ERROR)
                    {
                        string errType;
                        MessageParser.ParseNext(respMsg, out errType, out respMsg);

                        if (errType == Request.TURN)
                        {
                            string errMsg = respMsg;
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

            if (msg.StartsWith(Request.TURN)) {
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
             
                SendToAllPeers(msg);
            }
            else if (msg.StartsWith(Request.STRIKE))
            {
                SendToAllPeers(msg);
            }
            else if (msg == Request.QUIT)
            {
                msg += " " + myPeerInfo.PlayerInfo.PlayerId + " " + 0;
                SendToAllPeers(msg);

                Dispose();
            }
            else if (msg.StartsWith(Request.RECONNECTED))
            {
                SendToAllPeers(msg);
            }
            else
            {
                try { 
                SendToAllPeers(msg);
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

                do
                {

                
                    Console.WriteLine("Waiting for a connection...");
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

        private void StrikePlayer(int playerId)
        {
            PeerInfo playerToBeStriked = allPeersInfo.Where(peer => peer.PlayerInfo.PlayerId == playerId).First();

            if (playerToBeStriked.IsStrikeOutOnNextAdd())
            {

                RemovePeerFromGame(playerToBeStriked);
                Console.WriteLine("Player " + playerId + " has been removed due to unresponsiveness.");
            }
            else {

                int strikeNum = playerToBeStriked.AddStrike();

                Console.WriteLine("Player " + playerToBeStriked + " strike " + strikeNum);
            }
        }

        private void RemovePeerFromGame(PeerInfo peerToBeRemoved)
        {
            

            allPeersInfo.Remove(peerToBeRemoved);

            game.RemovePlayer(peerToBeRemoved.PlayerInfo);
           
            Console.WriteLine(game);

            bool iAmLeader = true;
            foreach (PeerInfo p in allPeersInfo)
            {
                if (p.PlayerInfo.PlayerId < myPeerInfo.PlayerInfo.PlayerId)
                {
                    iAmLeader = false;
                    break;
                }
            }

            if (iAmLeader)
            {
                TcpClient toServerClient = new TcpClient();
                /*TODO Send to server to update game session*/
                
            }
        }

        public void Dispose()
        {
            _peerListener.Stop();

            allPeersInfo = null;
        }

    }
}
