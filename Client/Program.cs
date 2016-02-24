using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public ClientProgram(string player = "NewPlayer")
        {
            connectToServer();
            playerName = player;
        }

        private void connectToServer()
        {
            client = new TcpClient();
            Console.WriteLine("Connecting.....");

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
            connectToServer();
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
                    }) ;

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
                ClientProgram aClient = new ClientProgram(pName);
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
