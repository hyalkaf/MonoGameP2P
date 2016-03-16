using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;

namespace Server
{
    public class ServerProgram
    {
        const string REQ_GAME = "game";
        const string REQ_PLAYERS = "players";
        const string REQ_CANCEL = "cancel";
        const string REQ_IP = "ip";
        const string RESP_SUCCESS = "success";

        private ReplicationManager rm;
        public IPAddress ipAddr;
        private IPAddress primaryIPAddress;
        private List<string> playerQueue;
        private Object thisLock = new Object();
        
        private List<Socket> sockets;
        public bool isPrimaryServer = false;
        private int portNumber = 9000;

        // Will use index as number of clients who want to be matched with this amount of other clients
        // then once that index has fullfilled its number we will match those in that index to a game.
        private List<Dictionary<string, Socket>> socketsForGameRequests;

        private TcpListener listener;
        private AutoResetEvent are = new AutoResetEvent(true);

        public ServerProgram()
        {
            sockets = new List<Socket>();
            socketsForGameRequests = new List<Dictionary<string, Socket>>();
            playerQueue = new List<string>();

            /* Initializes the Listener */            
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
            ipAddr =  IPAddress.Parse(localIP);

            // Change ipaddress of primary in case 
            readServerStatusFromConsole();

            // Initalize a replica and make it listen
            rm = new ReplicationManager(this, primaryIPAddress);

            // Initalize a listening port for replication Manager.
            // TODO: Might need to change the way this code is being called. 
            // new Task(() => { rm.ListenReplica(); }).Start();

            listener = new TcpListener(ipAddr, 8001);
        }

        /// <summary>
        /// This method gets information from console about status of server and their IP addresses
        /// </summary>
        private void readServerStatusFromConsole()
        {
            // Messages to the console when server starts
            Console.WriteLine("SERVER STARTED! This address is: " + ipAddr);
            Console.WriteLine("Set this to primary? (Y/N)");

            // Get user input and for either Yes or No and deal with other inputs
            string getInput = Console.ReadLine().Trim().ToUpper();
            while (getInput != "Y" || getInput != "N")
            {
                Console.WriteLine("Input is wrong, please indicate if this is the primary server or Not by inputing Y or N? ");
                getInput = Console.ReadLine().Trim().ToUpper();
            }

            // Check that what the user has input
            IPAddress ipaddress = ipAddr;
            if (getInput == "Y")
            {
                isPrimaryServer = true;
                ipAddr = ipaddress;
                primaryIPAddress = ipaddress;
            }
            else if (getInput == "N")
            {
                Console.WriteLine("What is the IP address of the primary server? ");
                string getIP = Console.ReadLine();
                
                while (!IPAddress.TryParse(getIP, out ipaddress))
                {
                    Console.WriteLine("There was a mistake in your IP address input. What is the IP address of the primary server in the form x.x.x.x? ");
                    getIP = Console.ReadLine();
                }
                primaryIPAddress = ipaddress;
            }
            // Code shouldn't hit else part
            else
            {
                Console.WriteLine("ERROR");
            }

        }

        void EstablishConnection(Socket s, int id)
        {
            StringBuilder sb = new StringBuilder();
            sockets.Add(s);

            Console.WriteLine("Connection accepted from "); //+ ipaddr + " : " + portNumber);

            byte[] buffer = new byte[2048];
            int bytesRead = s.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            string requestMessage = sb.ToString().Trim().ToLower();
            string requestType = "";
            if (requestMessage.IndexOf(" ") == -1)
            {
                requestType = requestMessage;
            }
            else
            {
                requestType = requestMessage.Substring(0, requestMessage.IndexOf(" ")).Trim();
            }
            
            requestMessage = requestMessage.Substring(requestType.Length).Trim();
            //Console.WriteLine("Recieved...");

            Console.WriteLine(requestMessage);
            string responseMessage = "error I DO NOT UNDERSTAND THIS REQUEST";

            if (requestType == REQ_GAME)
            {
                // All the data has been read from the 
                // client. Display it on the console.
                string pName = requestMessage.Substring(0, requestMessage.IndexOf(" "));

                // TODO: Deal with cases where parsing doesn't work
                int numberOfPeers = int.Parse(requestMessage.Substring(pName.Length));

                // Add this socket to the match making list of list of sockets
                if (numberOfPeers >= socketsForGameRequests.Count)
                {
                    for (int i = 0; i <= numberOfPeers; i++)
                    {
                        socketsForGameRequests.Add(new Dictionary<string, Socket>());
                    }                    
                }

                socketsForGameRequests[numberOfPeers][pName] = s;

            }
            else if (requestType == REQ_PLAYERS)
            {
                responseMessage = RESP_SUCCESS + " " + REQ_PLAYERS + "  " + sockets.Count;
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                ASCIIEncoding asen = new ASCIIEncoding();

                byte[] b = asen.GetBytes(responseMessage + "\n\n");

                Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

                s.Send(b);

                Console.WriteLine("\nSent Acknowledgement");
                if (sockets.Exists(soc => soc == s))
                {
                    sockets.Remove(s);
                }

            }
            else if (requestType == REQ_CANCEL)
            {
                // All the data has been read from the 
                // client. Display it on the console.
                responseMessage = RESP_SUCCESS + " " + REQ_CANCEL + " YOU CANCELED your match request.";

                // Echo the data back to the client.
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                ASCIIEncoding asen = new ASCIIEncoding();

                byte[] b = asen.GetBytes(responseMessage + "\n\n");

                Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

                s.Send(b);

                Console.WriteLine("\nSent Acknowledgement");
                if (sockets.Exists(soc => soc == s))
                {
                    sockets.Remove(s);
                }

            }


        }

        public void StartListen()
        {
            /* Start Listeneting at the specified port */

            listener.Start();

            Console.WriteLine("The server is running at port 8001...");
            Console.WriteLine("The local End point is  :" + listener.LocalEndpoint);
            

            int counter = 0;
            new Thread(() => {
                MatchPeers();
            }).Start();
            do
            {
                counter++;

                Console.WriteLine("Waiting for a connection {0} .....", counter);
                Socket s = listener.AcceptSocket();

                new Thread(() => {
                    EstablishConnection(s, counter);
                }).Start();
               
          
            } while (true);
            /* clean up */

          
        }

        private void MatchPeers()
        {
            while (true)
            {
                string responseMessage = string.Empty;

                for (int i = 0; i < socketsForGameRequests.Count; i++)
                {
                    // bypass first and second index since there are no matches with 0 or 1 player
                    if (i != 0 && i != 1)
                    {
                        // TODO: Will not work when in index 2 there are four want 2
                        if (i == socketsForGameRequests[i].Count)
                        {
                            // Do match between peers
                            foreach (KeyValuePair<string, Socket> dicNameToSocket in socketsForGameRequests[i])
                            {
                                // CH : Get endpoint 
                                IPEndPoint remoteIpEndPoint = dicNameToSocket.Value.RemoteEndPoint as IPEndPoint;

                                // CH : Get ip address from the endpoint
                                IPAddress ipaddr = remoteIpEndPoint.Address;

                                // Assign the ip address to a port
                                string thisClient = ipaddr + " " + portNumber++ + " " + dicNameToSocket.Key;

                                playerQueue.Add(thisClient);
                            }

                            string playersToBeSent = "";

                            for (int z = 0; z < playerQueue.Count; z++)
                            {
                                playersToBeSent += playerQueue[z] + " " + z;
                                if (z != playerQueue.Count - 1)
                                {
                                    playersToBeSent += ",";
                                }
                            }

                            responseMessage = RESP_SUCCESS + " " + REQ_GAME + " " + playersToBeSent;

                            foreach (KeyValuePair<string, Socket> dicNameToSocket in socketsForGameRequests[i])
                            {
                                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                                ASCIIEncoding asen = new ASCIIEncoding();

                                byte[] b = asen.GetBytes(responseMessage + "\n\n");

                                Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

                                dicNameToSocket.Value.Send(b);

                                Console.WriteLine("\nSent Acknowledgement");

                                dicNameToSocket.Value.Close();
                                sockets.Remove(dicNameToSocket.Value);

                            }

                            // TODO: Find a way to remove only the ones matched
                            socketsForGameRequests.Remove(socketsForGameRequests[i]);
                            playerQueue.Clear();
                        }

                    }
                }
            }
        }
        static void Main(string[] args)
        {
            try
            {
                ServerProgram svr = new ServerProgram();
                svr.StartListen();

            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR from server listening.....\n" + e.Message);
                Console.WriteLine(e.StackTrace);
                //Console.ReadLine();
            }
        }

    }

}
