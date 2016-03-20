using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace Server
{
    public class ServerProgram
    {
        public const string REQ_GAME = "game";
        public const string REQ_PLAYERS = "players";
        public const string REQ_CANCEL = "cancel";
        public const string REQ_IP = "ip";
        public const string REQ_CHECKNAME = "checkname";
        public const string REQ_RECONN = "reconn";
        public const string RESP_SUCCESS = "success";
        public const string RESP_FAILURE = "failure";
        public const string RESP_ERROR = "error";

        private ReplicationManager rm;
        public IPAddress ipAddr;
        private IPAddress primaryIPAddress;


        // Will use index as number of clients who want to be matched with this amount of other clients
        // then once that index has fullfilled its number we will match those in that index to a game.
        //private List<ConcurrentDictionary<string, Socket>> socketsForGameRequests;

        private List<ConcurrentQueue<ClientInfo>> clientsWaitingForGame;
        //private List<Socket> sockets;
        private List<ClientInfo> connectedClients;
        public List<string> allPlayerNamesUsed;
        public Dictionary<string, List<string>> gameSession;
        public bool isPrimaryServer = false;
        private int portNumber = 9000;
        private int gameIdGenerate = 1;

        private TcpListener listener;


        public ServerProgram()
        {

            connectedClients = new List<ClientInfo>();
            //sockets = new List<Socket>();
            //socketsForGameRequests = new List<ConcurrentDictionary<string, Socket>>();
            clientsWaitingForGame = new List<ConcurrentQueue<ClientInfo>>();

            allPlayerNamesUsed = new List<string>();
            gameSession = new Dictionary<string, List<string>>();
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
            while (getInput != "Y" && getInput != "N")
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

        void EstablishConnection(TcpClient tcpclient, int id)
        {
            // Socket s = tcpclient.Client;
            NetworkStream netStream = tcpclient.GetStream();
            //StringBuilder sb = new StringBuilder();
            //sockets.Add(tcpclient.Client);
            ClientInfo newConnectedClient = new ClientInfo(tcpclient);
            connectedClients.Add(newConnectedClient);

            Console.WriteLine("Connection accepted from client"); //+ ipaddr + " : " + portNumber);

            //byte[] buffer = new byte[2048];
            tcpclient.ReceiveBufferSize = 2048;
            byte[] bytes = new byte[tcpclient.ReceiveBufferSize];
          
            //int bytesRead;
            try { 
            //   bytesRead = s.Receive(buffer);
                netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);
            }
            catch (Exception)
            {
                connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                tcpclient.Close();
                // sockets.Remove(tcpclient.Client);


                return;
            //    s.Close();
            //    sockets.Remove(s);
            //    return;
            }



            //sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            //string requestMessage = sb.ToString().Trim().ToLower();
            string requestMessage = Encoding.ASCII.GetString(bytes).Trim();
            requestMessage = requestMessage.Substring(0, requestMessage.IndexOf("\0")).Trim();
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
            string responseMessage = RESP_ERROR +" Invalid Request Message";

            if (requestType == REQ_GAME)
            {
                // All the data has been read from the 
                // client. Display it on the console.
                
                string pName = requestMessage.Substring(0, requestMessage.IndexOf(" "));

                newConnectedClient.PlayerName = pName;

                int numberOfPeers = int.Parse(requestMessage.Substring(pName.Length));

                // Add this socket to the match making list of list of sockets
                //if (numberOfPeers >= socketsForGameRequests.Count)
                //{
                //    for (int i = socketsForGameRequests.Count; i <= numberOfPeers; i++)
                //    {
                //        socketsForGameRequests.Add(new ConcurrentDictionary<string, Socket>());
                //    }
                //}


                if (numberOfPeers >= clientsWaitingForGame.Count)
                {
                    for (int i = clientsWaitingForGame.Count; i <= numberOfPeers; i++)
                    {
                        clientsWaitingForGame.Add(new ConcurrentQueue<ClientInfo>());
                    }
                }


                // Name should be unique, otherwise change it
               // socketsForGameRequests[numberOfPeers][pName] = tcpclient.Client;
                clientsWaitingForGame[numberOfPeers].Enqueue(newConnectedClient);

                // Find game match
                MatchPeers();

            }
            else if (requestType == REQ_RECONN)
            {


                string gameId = requestMessage;
                if (gameSession.ContainsKey(gameId))
                {
                    responseMessage = RESP_SUCCESS + " " + REQ_RECONN + "  ";
                    for (int j = 0; j < gameSession[gameId].Count; j++)
                    {
                        if (j != gameSession[gameId].Count - 1)
                        {
                            responseMessage += gameSession[gameId][j] + ",";
                        }
                        else
                        {
                            responseMessage += gameSession[gameId][j];
                        }
                    }
                }
                else
                {
                    responseMessage = RESP_FAILURE + " " + REQ_RECONN + "  No such game exists";
                }
                
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                Console.WriteLine("\nSent Acknowledgement");

                if(connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }
                //if (sockets.Exists(soc => soc == tcpclient.Client))
                //{
                //    tcpclient.Close();
                //    sockets.Remove(tcpclient.Client);
                //}


            }
            else if (requestType == REQ_PLAYERS)
            {
                responseMessage = RESP_SUCCESS + " " + REQ_PLAYERS + "  " + connectedClients.Count;
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);




                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                Console.WriteLine("\nSent Acknowledgement");

                if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }

                //if (sockets.Exists(soc => soc == tcpclient.Client))
                //{
                //    tcpclient.Close();
                //    sockets.Remove(tcpclient.Client);
                //}

            }
            else if (requestType == REQ_CANCEL)
            {
                // All the data has been read from the 
                // client. Display it on the console.
                responseMessage = RESP_SUCCESS + " " + REQ_CANCEL + " YOU CANCELED your match request.";

                // Echo the data back to the client.
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);


                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                Console.WriteLine("\nSent Acknowledgement");
                if(connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }
                //if (sockets.Exists(soc => soc == tcpclient.Client))
                //{
                //    tcpclient.Close();
                //    sockets.Remove(tcpclient.Client);
                //}

            }
            else if (requestType == REQ_CHECKNAME)
            {
                var aPlayerName = requestMessage;
                if (allPlayerNamesUsed.IndexOf(aPlayerName) == -1)
                {
                    responseMessage = RESP_SUCCESS + " " + REQ_CHECKNAME + "  This name is not taken";
                    allPlayerNamesUsed.Add(aPlayerName);
                }
                else
                {
                    responseMessage = RESP_FAILURE + " " + REQ_CHECKNAME + "  This name is taken";
                }
                
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                Console.WriteLine("\nSent Acknowledgement");

                if(connectedClients.Exists(client => client == newConnectedClient))
                {
                    connectedClients.Remove(newConnectedClient);
                    tcpclient.Close();
                }

                //if (sockets.Exists(soc => soc == tcpclient.Client))
                //{
                //    tcpclient.Close();
                //    sockets.Remove(tcpclient.Client);
                //}

            }

        }

        public void StartListen()
        {
            /* Start Listeneting at the specified port */
            listener = new TcpListener(ipAddr, 8001);
            listener.Start();

            Console.WriteLine("The server is running at port 8001...");
            Console.WriteLine("The local End point is  :" + listener.LocalEndpoint);
            

            int counter = 0;
            do
            {
                Console.WriteLine("Waiting for a connection {0} .....", ++counter);
                
                TcpClient tcpclient = listener.AcceptTcpClient();
                //Socket s = listener.AcceptSocket();
                

                new Thread(() => {
                    EstablishConnection(tcpclient, counter);
                }).Start();
               
          
            } while (true);
            /* clean up */

          
        }
        private void MatchPeers()
        {
            string responseMessage = string.Empty;
            string playersToBeSent = "";
            List<string> playersToBeSentList = new List<string>();
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                // bypass first and second index since there are no matches with 0 or 1 player
                if (i != 0 && i != 1)
                {
                    // TODO: Will not work when in index 2 there are four want 2
                    if (i <= clientsWaitingForGame[i].Count)
                    {
                        
                        int stillConnected = clientsWaitingForGame[i].Count;


                        foreach (ClientInfo client in clientsWaitingForGame[i]){
                            byte[] testMsg = new byte[1];
                            try { 
                                client.TcpClient.Client.Send(testMsg, 0, 0);
                            }
                            catch (Exception)
                            {
                                stillConnected--;

                                if (client.TcpClient.Client.Connected) { 
                                    connectedClients.Remove(connectedClients.Where(c => c.TcpClient == client.TcpClient).First());
                                    client.TcpClient.Close();
                                }

                            }


                            Console.WriteLine("DEBUG: client " + client.PlayerName + client.IPAddr + " connected is " + client.TcpClient.Client.Connected);
                        }

                        if (stillConnected < i) return;

     
                        List<ClientInfo> readyToPlay = new List<ClientInfo>();
                        // Do match between peers
                        for (int playerId = 0; playerId < i; playerId++)
                        {
                            ClientInfo client;
                            bool dequeued;
                            do
                            {
                                dequeued = clientsWaitingForGame[i].TryDequeue(out client);
                            } while (!dequeued);

                            // Assign the ip address to a port
                            if (client.TcpClient.Client.Connected) { 
                                string clientPeerInfoMsg = client.IPAddr + " " + portNumber++ + " " + client.PlayerName + " " + playerId;

                                playersToBeSent += clientPeerInfoMsg + ",";
                                playersToBeSentList.Add(clientPeerInfoMsg);

                                readyToPlay.Add(client);
                            }
                            else
                            {
                                playerId--;
                            }

                        }

                        responseMessage = RESP_SUCCESS + " " + REQ_GAME + " " + playersToBeSent;
                       
                        Object listLock = new Object();

                        Parallel.ForEach(readyToPlay, (client) =>
                        {
                            Console.WriteLine("DEBUG: Response sent: " + responseMessage);
                            NetworkStream netStream = client.TcpClient.GetStream();

                            byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                            netStream.Write(byteToSend, 0, byteToSend.Length);

                            Console.WriteLine("\nSent Acknowledgement");

                            lock (listLock) { 
                                connectedClients.Remove(connectedClients.Where(c => c.TcpClient == client.TcpClient).First());

                                client.TcpClient.Close();
                            }
                        });

                        //  Save game session for reconnect
                        gameSession.Add(gameIdGenerate.ToString(), playersToBeSentList);
                        gameIdGenerate++;

                    }
                }
            }
        }
        //private void MatchPeers()
        //{
        //    string responseMessage = string.Empty;
        //    string playersToBeSent = "";
        //    List<string> playersToBeSentList = new List<string>();
        //    List<string> playerQueue = new List<string>();
        //    for (int i = 0; i < socketsForGameRequests.Count; i++)
        //    {
        //        // bypass first and second index since there are no matches with 0 or 1 player
        //        if (i != 0 && i != 1)
        //        {
        //            // TODO: Will not work when in index 2 there are four want 2
        //            if (i == socketsForGameRequests[i].Count)
        //            {
        //                // Do match between peers
        //                foreach (KeyValuePair<string, Socket> dicNameToSocket in socketsForGameRequests[i])
        //                {
        //                    // CH : Get endpoint 
        //                    IPEndPoint remoteIpEndPoint = dicNameToSocket.Value.RemoteEndPoint as IPEndPoint;

        //                    // CH : Get ip address from the endpoint
        //                    IPAddress ipaddr = remoteIpEndPoint.Address;

        //                    // Assign the ip address to a port
        //                    string thisClient = ipaddr + " " + portNumber++ + " " + dicNameToSocket.Key;

        //                    playerQueue.Add(thisClient);
        //                }

        //                for (int z = 0; z < playerQueue.Count; z++)
        //                {
        //                    playersToBeSent += playerQueue[z] + " " + z;
        //                    playersToBeSentList.Add(playerQueue[z] + " " + z);
        //                    if (z != playerQueue.Count - 1)
        //                    {
        //                        playersToBeSent += ",";
        //                    }
        //                }

        //                responseMessage = RESP_SUCCESS + " " + REQ_GAME + " " + playersToBeSent;

        //                foreach (KeyValuePair<string, Socket> dicNameToSocket in socketsForGameRequests[i])
        //                {
        //                    Console.WriteLine("DEBUG: Response sent: " + responseMessage);

        //                    ASCIIEncoding asen = new ASCIIEncoding();

        //                    byte[] b = asen.GetBytes(responseMessage + "\n\n");

        //                    Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

        //                    dicNameToSocket.Value.Send(b);

        //                    Console.WriteLine("\nSent Acknowledgement [Game Matched!]");
        //                    Console.WriteLine();
        //                    dicNameToSocket.Value.Close();
        //                    //sockets.Remove(dicNameToSocket.Value);
        //                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient.Client == dicNameToSocket.Value).First());

        //                }

        //                //  Save game session for reconnect
        //                gameSession.Add(gameIdGenerate.ToString(), playersToBeSentList);
        //                gameIdGenerate++;
        //                // TODO: Find a way to remove only the ones matched
        //                socketsForGameRequests.Remove(socketsForGameRequests[i]);
        //                playerQueue.Clear();
        //            }

        //        }

        //    }

        static void Main(string[] args)
        {
            try
            {
                ServerProgram svr = new ServerProgram();
                if (svr.isPrimaryServer)
                {
                    svr.StartListen();
                }

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
