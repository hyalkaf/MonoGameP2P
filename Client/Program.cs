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
    public class ClientProgram
    {
        TcpClient client;
        private string playerName;
        private bool inGame = false;
        public const string REQ_GAME = "game";
        public const string REQ_PLAYERS = "players";
        public const string REQ_CANCEL = "cancel";
        public const string SERVER_IP = "127.0.0.1";
        const string RESP_SUCCESS = "success";

        // Fileds for Peers
        private List<TcpClient> _peerSender;
        private TcpListener _peerListener;
        private Dictionary<int, int> peersIDToPosition;
        private int portSender;
        private int portListener;
        const string TURN = "turn";
        const string QUIT = "quit";

        public ClientProgram(int portSender, int portListener, string player = "NewPlayer")
        {
            connectToServer();
            playerName = player;
            this.portListener = portListener;
            this.portSender = portSender;
        }

        private void connectToServer()
        {
            client = new TcpClient();
            Console.WriteLine("Connecting.....");

            // use the ipaddress as in the server program
          
            client.Connect(SERVER_IP, 8001);
            
            Console.WriteLine("Connected");
            
        }

        private void inializePeers()
        {
            _peerSender = new List<TcpClient>();
            _peerListener = new TcpListener(IPAddress.Any, portListener);
            peersIDToPosition = new Dictionary<int, int>();
        }


        public void SendRequest(string msg = "")
        {

            string reqMessage = msg;


            if(reqMessage == REQ_GAME || reqMessage == REQ_CANCEL)
            {
                reqMessage += " " + playerName;

            }

            reqMessage += "\n\n";
            Stream stm = client.GetStream();

            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] ba = asen.GetBytes(reqMessage);

            Console.WriteLine("Transmitting your request to the server.....\n");
            stm.Write(ba, 0, ba.Length);

            byte[] bb = new byte[2048];
            Console.WriteLine("Waiting");
            int k = stm.Read(bb, 0, 2048);

            string responseMessage = "";
            char c = ' ';
            for (int i = 0; i < k; i++)
            {
                c = Convert.ToChar(bb[i]);
                responseMessage += c;
            }

           ;

            if (processResponse(responseMessage) == -1) {
                Console.WriteLine("\nDEBUG: INVALID REQUEST/RESPONSE\n");
            }

            client.Close();
            if (!inGame) {
                connectToServer();
            }
        }

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
                        EstablishConnectionPeers(s, counter);
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

        void EstablishConnectionPeers(Socket s, int id)
        {
            StringBuilder sb = new StringBuilder();
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
                peersIDToPosition[numberOne] += numberTwo;

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
                peersIDToPosition[numberOne] += numberTwo;
            }


            ASCIIEncoding asen = new ASCIIEncoding();

            byte[] b = asen.GetBytes(responseMessage + "\n\n");

            Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

            s.Send(b);

            Console.WriteLine("\nSent Acknowledgement");

        }

        private int processResponse(string responseMessage)
        {

            responseMessage = responseMessage.Trim();
   
            if (responseMessage.StartsWith(RESP_SUCCESS) /*&& responseMessage.EndsWith("\n\n")*/)
            {
                responseMessage = responseMessage.Substring(RESP_SUCCESS.Length).Trim();

                string requestType = responseMessage.Substring(0, responseMessage.IndexOf(" ")).Trim();
                responseMessage = responseMessage.Substring(requestType.Length);

                Console.WriteLine("\nDEBUG: " + requestType+"\n");
                if (requestType == REQ_GAME)
                {
                    string[] addressList = responseMessage.Split(',');
                    new Thread(() => {
                        //PROCESS p2p CONNECTION USING THE ADDRESS LIST ABOVE
                        inializePeers();
                        new Thread(() => {
                            Console.WriteLine("Connected Peers");
                            StartListenPeers();
                            
                        });   
                    });

                    inGame = true;
                    return 0;
                }
                else if(requestType == REQ_PLAYERS)
                {
                    // DISPLAY playernum ON GUI
                    string playernum = responseMessage;
                    return 0;
                }
                else if (requestType == REQ_CANCEL)
                {
                    // INDICATES THAT THE USER HAVE CANCELED 
                    return 0;
                }
                
            }
           
            return -1;
            
        }

        public void startClient()
        {
            try { 

                while (!inGame) {

                    Console.Write("Send request (game, players, cancel): ");
                    var request = Console.ReadLine().Trim().ToLower();

                    Thread t = new Thread(() => {
                        Console.WriteLine("Sending request \" {0} \"", request);
                        SendRequest(request);
        
                    });

                    t.Start();
                }
            } catch (Exception e) {
                Console.WriteLine("Something Wrong");
                client.Close();
            };
        }

        static void Main(string[] args)
        {
            try
            {
                Console.Write("Enter IGN: ");
                string pName = Console.ReadLine();
                Console.Write("Enter the port of the sender: ");
                int portSender = int.Parse(Console.ReadLine());
                Console.Write("Enter the port to listen to: ");
                int portListener = int.Parse(Console.ReadLine());
                ClientProgram aClient = new ClientProgram(portSender, portListener, pName);
                aClient.startClient();


                Console.Write("--See you next time--");
                Console.Read();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
        }
    }
}
