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
        private PeerInfo myPeerInfo;
        private List<PeerInfo> allPeersInfo;

        // Initalize variables for peer(client) connecting to other peers(clients)
        private string playerName;
        private TcpListener _peerListener;
        private TcpClient[] _peerSender;
        // Peerinfo <ip, port, playername, playerID, timebeforekick>
       // private List<Tuple<string, int, string, int, int>> peersInfo;
        private Dictionary<int, int> peersIDToPosition;
        public const string REQ_TURN = "turn";
        public const string REQ_QUIT = "quit";
        public const string REQ_SUCCESS = "success";
        public const string REQ_RECONNECTED = "reconnected";
        public const string REQ_STRIKE = "strike";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        //public Peer(string playerName, List<Tuple<string, int, string, int, int>> peersInfo)
        public Peer(string playerName, List<PeerInfo> peersInfo)
        {
            
            Console.WriteLine("PEER ESTABLISHED!!");

            allPeersInfo = peersInfo;
            
            this.playerName = playerName;
            Console.WriteLine("For " + playerName);

            // TODO: Initialize variables to hold other IP Addresses and ports for other peers.
            // Check if peersInfo is populated
            if (allPeersInfo.Count > 0)
            {
                _peerSender = new TcpClient[allPeersInfo.Count];

                // Get this peerInfo
                // TODO: deal with empty or not existent peer
                //Tuple<string, int, string, int, int> tempPeer = peersInfo.Where(peer => peer.Item3 == playerName).First();
                myPeerInfo = allPeersInfo.Where(peer => peer.PlayerInfo.Name == playerName).First();

                IPHostEntry host;
                string localIP = "";
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                    }
                }
                IPAddress ipAddr = IPAddress.Parse(localIP);

                _peerListener = new TcpListener(ipAddr, myPeerInfo.Port);
            }

            peersIDToPosition = new Dictionary<int, int>();
        }

        public void startPeerCommunication()
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
                    if (req.StartsWith(REQ_TURN) || req.StartsWith(REQ_QUIT))
                    {
                        SendRequestPeers(req);
                    }
                    else
                    {
                        Console.WriteLine("INVALID INPUT (turn or quit)");
                    }

                    if (req.Trim().ToLower() == REQ_QUIT)
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
        void EstablishConnection(Socket s, int id)
        {
            Console.WriteLine("CONNECTED WITH YOU: " + s.RemoteEndPoint);
            Console.WriteLine();
            StringBuilder sb = new StringBuilder();

            byte[] buffer = new byte[2048];
            int bytesRead = s.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            string requestMessage = sb.ToString().Trim().ToLower();

            Console.WriteLine("DEBUG: Response: " + requestMessage);

            string responseMessage = "I DO NOT UNDERSTAND THIS REQUEST";

            // When a peer is broadcasting its turn
            if (requestMessage.StartsWith(REQ_TURN))
            {

                if (!peersIDToPosition.ContainsKey(id))
                {
                    peersIDToPosition.Add(id, 0);
                }

                responseMessage = REQ_SUCCESS + " " + REQ_TURN;

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

                Console.WriteLine("\nPlayer " + playerId + " (" + playerName +  ") move " + diceRolled + " steps.");

            }
            else if (requestMessage.StartsWith(REQ_QUIT))
            {
                if (!peersIDToPosition.ContainsKey(id))
                {
                    peersIDToPosition.Add(id, 0);
                }

                responseMessage = REQ_SUCCESS + " " + REQ_QUIT;

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
                //peersInfo.Remove (peersInfo.Where(peerInfo => peerInfo.Item4 == playerId).First());
                allPeersInfo.Remove(allPeersInfo.Where(peer => peer.PlayerInfo.PlayerId == playerId).First());
                _peerSender = new TcpClient[_peerSender.Length - 1];
                // Keep track of peers with their position
                // peersIDToPosition[numberOne] += numberTwo;
            }else if (requestMessage.StartsWith(REQ_STRIKE))
            {

                responseMessage = REQ_SUCCESS + " " + REQ_STRIKE;
                string trimmedMessage = requestMessage.Trim();
                string restOfMessageAfterStrike = trimmedMessage.Substring(REQ_STRIKE.Length);

                int playerId = int.Parse(restOfMessageAfterStrike.Trim());

                strikePlayer(playerId);
            }


            ASCIIEncoding asen = new ASCIIEncoding();

            byte[] b = asen.GetBytes(responseMessage + "\n\n");

            Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

            s.Send(b);

        }

        /// <summary>
        /// Send message to all peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        private void SendToALlPeers(string msg)
        {
            var responseCounterFlag = 0;
            int playerToBeStriked = -1;
            if (msg.StartsWith(REQ_STRIKE))
            {
                playerToBeStriked = int.Parse(msg.Substring(REQ_STRIKE.Length).Trim());

            }

            // Multicast message to all peers
            Parallel.For(0, _peerSender.Count(), i => {
                // Check if peersInfo is not you and then send info
               // if (peersInfo[i].Item3 != playerName && peersInfo[i].Item4 != playerToBeStriked)
               if(allPeersInfo[i].PlayerInfo.Name != myPeerInfo.PlayerInfo.Name &&
                allPeersInfo[i].PlayerInfo.PlayerId != playerToBeStriked)
                {
                    Console.WriteLine("Connecting to a peer.....");


                    bool succPeerConnect = true;
                    int numOfTries = 3;
                    do
                    {
                        _peerSender[i] = new TcpClient();
                        succPeerConnect = true;
                        try
                        {
                            // _peerSender[i].Connect(peersInfo[i].Item1, peersInfo[i].Item2);
                            _peerSender[i].Connect(allPeersInfo[i].IPAddr, allPeersInfo[i].Port);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Can't connect to peer " + allPeersInfo[i].PlayerInfo.PlayerId);
                            Console.WriteLine("Trying... Times to try: " + numOfTries);
                            _peerSender[i].Close();
                            succPeerConnect = false;
                            numOfTries--;
                            if (numOfTries == 0)
                            {
                                Console.WriteLine("Unable to communicate with peer " + allPeersInfo[i].PlayerInfo.PlayerId);
                                Console.WriteLine("Skip it for now...");
                                SendRequestPeers(REQ_STRIKE + " " + allPeersInfo[i].PlayerInfo.PlayerId);

                                strikePlayer(i);

                                return;
                            }
                        }

                    } while (!succPeerConnect && numOfTries > 0);

                    Console.WriteLine("Connected to the peer");

                    string reqMessage = msg;

                    Stream stm = _peerSender[i].GetStream();

                    ASCIIEncoding asen = new ASCIIEncoding();
                    byte[] ba = asen.GetBytes(reqMessage);
                    Console.WriteLine("Transmitting your request to the peer {0} .....\n", allPeersInfo[i].PlayerInfo.PlayerId);

                    stm.Write(ba, 0, ba.Length);

                    byte[] bb = new byte[2048];
                    Console.WriteLine("Waiting for response from peer {0}...", allPeersInfo[i].PlayerInfo.PlayerId);
                    int k = stm.Read(bb, 0, 2048);
                    string responseMessage = "";
                    char c = ' ';
                    for (int j = 0; j < k; j++)
                    {
                        c = Convert.ToChar(bb[j]);
                        responseMessage += c;
                    }


                    if (responseMessage.StartsWith(REQ_SUCCESS))
                    {
                        responseCounterFlag++;
                        Console.WriteLine("NUM OF RESPONSES " + responseCounterFlag);
                    }

                    _peerSender[i].Close();
                }

            });
        }

        /// <summary>
        /// Send/Handle request messages to peers
        /// 
        /// </summary>
        /// <param name="msg"></param>
        public void SendRequestPeers(string msg)
        {

            //int playerID = peersInfo.Where(elem => elem.Item3 == playerName).First().Item4;
           // int playerID = myPeerInfo.PlayerInfo.PlayerId;

            if (msg.StartsWith(REQ_TURN)) {
                Random rnd = new Random();
                int dice = rnd.Next(1,7);
                msg += " " + myPeerInfo.PlayerInfo.Name + " " +
                   myPeerInfo.PlayerInfo.PlayerId + " " + dice;

                SendToALlPeers(msg);
            }
            else if (msg.StartsWith(REQ_STRIKE))
            {
                SendToALlPeers(msg);
            }
            else
            {
                msg += " " + myPeerInfo.PlayerInfo.PlayerId + " " + 0;
                SendToALlPeers(msg);

                Dispose();
            }

         
                        
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
                    Socket s = _peerListener.AcceptSocket();

                    new Thread(() => {
                        EstablishConnection(s, counter);
                    }).Start();

                
                } while (true);

            }
            catch (Exception)
            {
                //Console.WriteLine(e.Message);
                _peerListener.Stop();
                //Console.WriteLine(e.StackTrace);
                Console.WriteLine("You have quit the game! Press Enter to Continue...");
            }

        }

        private void strikePlayer(int playerId)
        {
            int index = allPeersInfo.IndexOf(allPeersInfo.Where(peer => peer.PlayerInfo.PlayerId == playerId).First());
            //Tuple<string, int, string, int, int> peerInfo = allPeersInfo[index];
            //int strikeout = peerInfo.Item5 + 1;
            
            if (allPeersInfo[index].IsStrikeOutOnNextAdd())
            {
                allPeersInfo.RemoveAt(index);
                _peerSender = new TcpClient[_peerSender.Length - 1];
                Console.WriteLine("Player " + playerId + " has been removed due to unresponsiveness.");
            }
            else {

                //peerInfo = new Tuple<string, int, string, int, int>(
                //    peerInfo.Item1,
                //    peerInfo.Item2,
                //    peerInfo.Item3,
                //    peerInfo.Item4, 
                //    strikeout);

                allPeersInfo[index].AddStrike();

                Console.WriteLine("Player " + playerId + " strike " + allPeersInfo[index]);
            }
        }

        public void Dispose()
        {
            _peerListener.Stop();
            _peerSender = null;
            allPeersInfo = null;
            peersIDToPosition = null;


        }

    }
}
