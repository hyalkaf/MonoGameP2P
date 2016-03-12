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
        bool inGame = false;
        public const string REQ_GAME = "game";
        public const string REQ_PLAYERS = "players";
        public const string REQ_CANCEL = "cancel";
        //public const string SERVER_IP = "127.0.0.1";
        public const string SERVER_IP = "10.13.136.75";
        const string RESP_SUCCESS = "success";
        

        // Fileds for Peers
        private List<TcpClient> _peerSender;
        private TcpListener _peerListener;
        private Dictionary<int, int> peersIDToPosition;
        private int portSender;
        private int portListener;
        const string TURN = "turn";
        const string QUIT = "quit";
        List<Tuple<string, string, int>> peersInfo;

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
            Console.WriteLine("Connecting to Server.....");

            // use the ipaddress as in the server program
          
            client.Connect(SERVER_IP, 8001);
            
            Console.WriteLine("Connected");
            
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
        
        private int processResponse(string responseMessage)
        {

            responseMessage = responseMessage.Trim();

            if (responseMessage.StartsWith(RESP_SUCCESS) /*&& responseMessage.EndsWith("\n\n")*/)
            {
                responseMessage = responseMessage.Substring(RESP_SUCCESS.Length).Trim();

                string requestType = responseMessage.Substring(0, responseMessage.IndexOf(" ")).Trim();
                responseMessage = responseMessage.Substring(requestType.Length);

                Console.WriteLine("\nDEBUG: " + requestType + "\n");
                if (requestType == REQ_GAME)
                {
                    peersInfo = new List<Tuple<string, string, int>>();
                    IEnumerable<string> temp = responseMessage.Split(',');
                    peersInfo = temp.Where(elem => !string.IsNullOrEmpty(elem)).Select(info =>
                    {
                        string[] peerInfo = info.Trim().Split(' ');
                        Tuple<string, string, int> t = null;
                        if (!string.IsNullOrEmpty(info))
                        {
                            t =  new Tuple<string, string, int>(peerInfo[0], peerInfo[1], int.Parse(peerInfo[2])); 
                        }

                        return t;
                       
                    }).ToList();
                    
                    inGame = true;
                    return 0;
                }
                else if (requestType == REQ_PLAYERS)
                {
                    // DISPLAY playernum ON GUI
                    string playernum = responseMessage;
                    Console.WriteLine("\nNum of Players on server now: " + playernum);
                    return 0;
                }
                else if (requestType == REQ_CANCEL)
                {
                    // INDICATES THAT THE USER HAVE CANCELED 
                    Console.WriteLine("\nYou have CANCELED your match making.");
                    return 0;
                }

            }

            return -1;

        }

        public void startClient()
        {
            while (true) {
                try {
                    while (true) { 
                        if(!inGame) {

                            Console.Write("Send request (game, players, cancel): ");
                            var request = Console.ReadLine().Trim().ToLower();

                            Console.WriteLine("Sending request \"{0}\"", request);
                            SendRequest(request);
        
                        }

                        else
                        {
                            break;

                        }
                    }

                    Peer peer = new Peer(playerName, portSender, portListener,peersInfo);


                } catch (Exception e) {
                    Console.WriteLine("Something Wrong");
                    client.Close();
                    Console.Error.WriteLine(e.StackTrace);
                };
            }
        }

        static void Main(string[] args)
        {
            try
            {
                Console.Write("Enter Your Player Name: ");
                string pName = Console.ReadLine();
                Console.Write("Enter the port of the sender: ");
                int portSender = int.Parse(Console.ReadLine());
                Console.Write("Enter the port to listen to: ");
                int portListener = int.Parse(Console.ReadLine());
                ClientProgram aClient = new ClientProgram(portSender, portListener, pName);
                aClient.startClient();


                Console.Write("--Program terminated. See you next time!--");
                Console.Read();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
        }
    }
}
