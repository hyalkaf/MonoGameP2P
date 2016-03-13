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
        // Initalize variables for peer(client) connecting to other peers(clients)
        private string playerName;
        private TcpListener _peerListener;
        private TcpClient[] _peerSender;
        private List<Tuple<string, int, string, int>> peersInfo;
        private Dictionary<int, int> peersIDToPosition;
        const string REQ_TURN = "turn";
        const string REQ_QUIT = "quit";


        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        public Peer(string playerName, List<Tuple<string, int, string, int>> peersInfo)
        {
            Console.WriteLine("PEER ESTABLISHED!!");
            this.playerName = playerName;
            Console.WriteLine("For " + playerName);


            // TODO: Initialize variables to hold other IP Addresses and ports for other peers.
            // Check if peersInfo is populated
            if (peersInfo.Count > 0)
            {
                _peerSender = new TcpClient[peersInfo.Count];

                // Get this peerInfo
                // TODO: deal with empty or not existent peer
                Tuple<string, int, string, int> tempPeer = peersInfo.Where(peer => peer.Item3 == playerName).First();
                _peerListener = new TcpListener(IPAddress.Parse(tempPeer.Item1), tempPeer.Item2);
            }

            peersIDToPosition = new Dictionary<int, int>();

            this.peersInfo = peersInfo;

            new Thread(() => {
                Console.WriteLine("\nDEBUG: Peer listen start");
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
                }
                catch (Exception e)
                {
                    break;
                }
                
            }
            

        }

        void EstablishConnection(Socket s, int id)
        {
            Console.WriteLine("CONNECTED WITH YOU: " + s.RemoteEndPoint);
            Console.WriteLine();
            StringBuilder sb = new StringBuilder();
            Console.WriteLine("Connection accepted from " + s.RemoteEndPoint);

            byte[] buffer = new byte[2048];
            int bytesRead = s.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            string requestMessage = sb.ToString().Trim().ToLower();

            Console.WriteLine("DEBUG: Response: " + requestMessage);

            Console.WriteLine(requestMessage);
            string responseMessage = "I DO NOT UNDERSTAND THIS REQUEST";

            // When a peer is broadcasting its turn
            if (requestMessage.StartsWith(REQ_TURN))
            {

                if (!peersIDToPosition.ContainsKey(id))
                {
                    peersIDToPosition.Add(id, 0);
                }

                responseMessage = "TURN";

                // Parse the request message
                string trimmedMessage = requestMessage.Trim();
                List<char> restOfMessageAfterTurn = trimmedMessage.Substring(4).ToList();

                // Get the first number in the turn message
                int numberOne = (int)Char.GetNumericValue(restOfMessageAfterTurn.SkipWhile(ch =>
                    char.IsWhiteSpace(ch)).TakeWhile(ch => !char.IsWhiteSpace(ch)).First());

                // Get the second the number in the turn message
                int numberTwo = (int)Char.GetNumericValue(restOfMessageAfterTurn.SkipWhile(ch =>
                    char.IsWhiteSpace(ch)).SkipWhile(ch => !char.IsWhiteSpace(ch)).SkipWhile(ch =>
                    char.IsWhiteSpace(ch)).TakeWhile(ch => !char.IsWhiteSpace(ch)).First());


                // Keep track of peers with their position
                // peersIDToPosition[numberOne] += numberTwo;

            }
            else if (requestMessage.StartsWith(REQ_QUIT))
            {
                if (!peersIDToPosition.ContainsKey(id))
                {
                    peersIDToPosition.Add(id, 0);
                }

                responseMessage = "QUIT";

                // Parse the request message
                string trimmedMessage = requestMessage.Trim();
                List<char> restOfMessageAfterTurn = trimmedMessage.Substring(4).ToList();

                // Get the first number in the turn message
                int numberOne = (int)Char.GetNumericValue(restOfMessageAfterTurn.SkipWhile(ch =>
                    char.IsWhiteSpace(ch)).TakeWhile(ch => !char.IsWhiteSpace(ch)).First());

                // Get the second the number in the turn message
                int numberTwo = (int)Char.GetNumericValue(restOfMessageAfterTurn.SkipWhile(ch =>
                    char.IsWhiteSpace(ch)).SkipWhile(ch => !char.IsWhiteSpace(ch)).SkipWhile(ch =>
                    char.IsWhiteSpace(ch)).TakeWhile(ch => !char.IsWhiteSpace(ch)).First());



                // Keep track of peers with their position
                // peersIDToPosition[numberOne] += numberTwo;
            }


            ASCIIEncoding asen = new ASCIIEncoding();

            byte[] b = asen.GetBytes(responseMessage + "\n\n");

            Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

            s.Send(b);

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        public void SendRequestPeers(string msg = "")
        {

            int playerID = peersInfo.Where(elem => elem.Item3 == playerName).First().Item4;

            if (msg.StartsWith(REQ_TURN)) {
                Random rnd = new Random();
                int dice = rnd.Next(1,7);
                msg += " " + peersInfo.Where(elem => elem.Item3 == playerName).First().Item3 + " " +
                   playerID + " " + dice;
            }
            else
            {
                msg += " " + playerID + " " + 0;
            }

            for (int i = 0; i < _peerSender.Count(); i++)
            {
                // Check if peersInfo is not you and then send info
                if (peersInfo[i].Item3 != playerName)
                {
                    Console.WriteLine("Connecting.....");

                    _peerSender[i] = new TcpClient();
                    _peerSender[i].Connect(peersInfo[i].Item1, peersInfo[i].Item2);

                    Console.WriteLine("Connected");

                    String reqMessage = msg;
                    if (msg == "")
                    {
                        Console.Write("Request message was empty, please re-enter: ");

                        reqMessage = Console.ReadLine();
                    }

                    Stream stm = _peerSender[i].GetStream();

                    ASCIIEncoding asen = new ASCIIEncoding();
                    byte[] ba = asen.GetBytes(reqMessage);
                    Console.WriteLine("Transmitting your request to the other peers.....\n");

                    stm.Write(ba, 0, ba.Length);

                    _peerSender[i].Close();
                }
            }

        }

        public void StartListenPeers()
        {
            /* Start Listeneting at the specified port */
            try { 
                _peerListener.Start();

                Console.WriteLine("The peer is running at port {0}...", (_peerListener.LocalEndpoint as IPEndPoint).Port);
                Console.WriteLine("The local End point is  :" +
                                  _peerListener.LocalEndpoint);
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                _peerListener.Stop();
                Console.WriteLine(e.StackTrace);
            }

        }

        public void Dispose()
        {
            _peerListener.Stop();
            _peerSender = null;
            peersInfo = null;
            peersIDToPosition = null;

    }
    }
}
