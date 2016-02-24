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
        private List<string> _peers;
        private Dictionary<int, int> peersIDToPosition;
        private List<Socket> _peersSockets;
        private ManualResetEvent _matchingMRE;
        const string TURN = "turn";
        const string QUIT = "quit";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        public Peer(List<string> peersIPAddress)
        {
            /* Initializes the Listener */
            _peerListener = new TcpListener(IPAddress.Any, 8001);


        }

        void EstablishConnection(Socket s, int id)
        {
            StringBuilder sb = new StringBuilder();
            _peersSockets.Add(s);
            Console.WriteLine("Connection accepted from " + s.RemoteEndPoint);

            byte[] buffer = new byte[2048];
            int bytesRead = s.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            String requestMessage = sb.ToString().Trim().ToLower();

            //Console.WriteLine("Recieved...");

            Console.WriteLine(requestMessage);
            string responseMessage = "I DO NOT UNDERSTAND THIS REQUEST";

            // When a peer is broadcasting its turn
            if (requestMessage.StartsWith(TURN))
            {

                if(!peersIDToPosition.ContainsKey(id))
                {
                    peersIDToPosition.Add(id, 0); 
                }

                responseMessage = "TURN";

                // Parse the request message
                string trimmedMessage = requestMessage.Trim();
                string restOfMessageAfterTurn = trimmedMessage.Substring(4);

                // Get the first number in the turn message
                string peerID = restOfMessageAfterTurn.SkipWhile(ch =>
                    Char.IsWhiteSpace(ch)).TakeWhile(ch => !Char.IsWhiteSpace(ch)).ToString();

                // Get the second the number in the turn message
                string peerNumberOfMoves = restOfMessageAfterTurn.SkipWhile(ch =>
                    Char.IsWhiteSpace(ch)).SkipWhile(ch => !Char.IsWhiteSpace(ch)).SkipWhile(ch =>
                    Char.IsWhiteSpace(ch)).TakeWhile(ch => !Char.IsWhiteSpace(ch)).ToString();

                // Parse the both numbers to an integer
                int numberOne = -1;
                int numberTwo = 0;

                Int32.TryParse(peerID, out numberOne);
                if(numberOne == -1)
                {
                    Console.WriteLine("Problem Happend with parsing turn number one");
                }

                Int32.TryParse(peerNumberOfMoves, out numberTwo);
                if (numberTwo == 0)
                {
                    Console.WriteLine("Problem Happend with parsing turnn number two");
                }

                // Keep track of peers with their position
                peersIDToPosition[numberOne] += numberTwo;

            }
            else if (requestMessage.StartsWith(QUIT))
            {
                
                // Parse the request message
                string trimmedMessage = requestMessage.Trim();
                string restOfMessageAfterTurn = trimmedMessage.Substring(4);

                // Get the first number in the turn message
                string peerID = restOfMessageAfterTurn.SkipWhile(ch =>
                    Char.IsWhiteSpace(ch)).TakeWhile(ch => !Char.IsWhiteSpace(ch)).ToString();

                // Get the second the number in the turn message
                string peerNumberOfMoves = restOfMessageAfterTurn.SkipWhile(ch =>
                    Char.IsWhiteSpace(ch)).SkipWhile(ch => !Char.IsWhiteSpace(ch)).SkipWhile(ch =>
                    Char.IsWhiteSpace(ch)).TakeWhile(ch => !Char.IsWhiteSpace(ch)).ToString();

                // Parse the both numbers to an integer
                int numberOne = -1;
                int numberTwo = 0;

                Int32.TryParse(peerID, out numberOne);
                if (numberOne == -1)
                {
                    Console.WriteLine("Problem Happend with parsing turn number one");
                }

                Int32.TryParse(peerNumberOfMoves, out numberTwo);
                if (numberTwo == 0)
                {
                    Console.WriteLine("Problem Happend with parsing turnn number two");
                }

                // Keep track of peers with their position
                peersIDToPosition[numberOne] += numberTwo;
            }
           

            ASCIIEncoding asen = new ASCIIEncoding();

            byte[] b = asen.GetBytes(responseMessage + "\n\n");

            Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

            s.Send(b);

            Console.WriteLine("\nSent Acknowledgement");

        }


        public void StartListen()
        {
            /* Start Listeneting at the specified port */
            _peerListener.Start();

            Console.WriteLine("The peer is running at port 8001...");
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        public void SendRequest(string msg = "")
        {
            Parallel.ForEach(_peerSender, ps =>
            { 
                ps = new TcpClient();
                Console.WriteLine("Connecting.....");

                // use the ipaddress as in the server program
                ps.Connect("127.0.0.1", 8001);

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

    }
}
