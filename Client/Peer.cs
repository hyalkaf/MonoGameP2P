using System;
using System.Collections;
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
    public class Peer
    {
        // Initalize variables for peer(client) connecting to other peers(clients)
        private TcpListener _peerListener;
        private List<TcpClient> _peerSender;
        private List<Tuple<string, string, int>>  peersInfo;
        private Dictionary<int, int> peersIDToPosition;
        private int portSender;
        private int portListener;
        const string TURN = "turn";
        const string QUIT = "quit";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        public Peer(string playerName, int portSender, int portListener, List<Tuple<string, string, int>> peersInfo)
        {
            /* Initializes the Listener */


            _peerSender = new List<TcpClient>();
            _peerSender.Add(null);

            _peerListener = new TcpListener(IPAddress.Any, portListener);
            peersIDToPosition = new Dictionary<int, int>();

            this.peersInfo = peersInfo;
            this.portListener = portListener;
            this.portSender = portSender;

            new Thread(() => {
                Console.WriteLine("\nDEBUG: Peer listen start");
                StartListenPeers();
            }).Start();


            while (true)
            {
                Console.Write("Enter request (turn, quit): ");
                string req = Console.ReadLine();
                req += " " + peersInfo.Where(elem => elem.Item2 == playerName).First().Item3 + " " + 0;

                SendRequestPeers(req);
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
            if (requestMessage.StartsWith(TURN))
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
            else if (requestMessage.StartsWith(QUIT))
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

            Parallel.ForEach(_peerSender, ps =>
            {
                ps = new TcpClient();
                Console.WriteLine("Connecting.....");

                // use the ipaddress as in the server program
                ps.Connect("127.0.0.1", portSender);

                Console.WriteLine("Connected");

                String reqMessage = msg;
                if (msg == "")
                {
                    Console.Write("Request message was empty, please re-enter: ");

                    reqMessage = Console.ReadLine();
                }

                Stream stm = ps.GetStream();

                ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(reqMessage);
                Console.WriteLine("Transmitting your request to the server.....\n");

                stm.Write(ba, 0, ba.Length);

                //byte[] bb = new byte[2048];
                //Console.WriteLine("Waiting");
                //int k = stm.Read(bb, 0, 2048);

                ps.Close();
            });
        }

        public void StartListenPeers()
        {
            /* Start Listeneting at the specified port */
            _peerListener.Start();

            Console.WriteLine("The peer is running at port {0}...", portListener);
            Console.WriteLine("The local End point is  :" +
                              _peerListener.LocalEndpoint);
 
            int counter = 0;
            do
            {
                counter++;
                try
                {
                    Console.WriteLine("Waiting for a connection {0} .....", counter);
                    Socket s = _peerListener.AcceptSocket();

                    new Thread(() => {
                        EstablishConnection(s, counter);
                    }).Start();

                }
                catch (Exception e)
                {
                    Console.WriteLine("Something went wrong!");
                    _peerListener.Stop();
                    Console.WriteLine(e.StackTrace);
                }
            } while (true);
            /* clean up */

        }
    }
}
