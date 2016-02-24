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
        public const string REQ_GAME = "game";
        public const string REQ_PLAYERS = "players";
        public const string REQ_CANCEL = "cancel";

        public ClientProgram(string player = "NewPlayer")
        {
            playerName = player;
            
        }

        private void connectToServer()
        {
            client = new TcpClient();
            Console.WriteLine("Connecting.....");

            // use the ipaddress as in the server program
          
            client.Connect("127.0.0.1", 8001);
            
            Console.WriteLine("Connected");
            
        }

        public void SendRequest(string msg = "")
        {
            connectToServer();
            String reqMessage = msg;
            if (msg == "")
            {
                Console.Write("Request message was empty, please re-enter: ");

                reqMessage = Console.ReadLine();
            }

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

            string result = parseResponse(responseMessage);
            if (result != null) {
                Console.WriteLine("\nDEBUG: RESPONSE:\n" + result);
            }
            else
            {
                Console.WriteLine("\nDEBUG: INVALID REQUEST/RESPONSE\n");
            }
           

            client.Close();
        }

        private string parseResponse(string responseMessage)
        {

            responseMessage = responseMessage.Trim();
   
            if (responseMessage.StartsWith("success"))
            {
                responseMessage = responseMessage.Substring("success".Length).Trim();

                string requestType = responseMessage.Substring(0, responseMessage.IndexOf(" ")).Trim();
                responseMessage = responseMessage.Substring(requestType.Length);

                Console.WriteLine("\nDEBUG: " + requestType+"\n");
                if (requestType == REQ_GAME)
                {
                    return responseMessage;
                }
                else if(requestType == REQ_PLAYERS)
                {
                    return responseMessage;
                }
                else if (requestType == REQ_CANCEL)
                {
                    return responseMessage;
                }

                
            }
           
            return null;
            
        }

        public void startClient()
        {
            try { 
                while (true) {
                   
                    Console.Write("Send request (game, players, cancel): ");
                    var request = Console.ReadLine().Trim().ToLower();

                    Thread t = new Thread(() => {
                        Console.WriteLine("Sending requestion \" {0} \"", request);
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
