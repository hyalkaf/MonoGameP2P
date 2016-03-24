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
        public class Request
        {
            public const string GAME = "game";
            public const string PLAYERS = "players";
            public const string CANCEL = "cancel";
            public const string IP = "ip";
            public const string CHECKNAME = "checkname";
            public const string RECONN = "reconn";
            public const string SERVRECONN = "servreconn";
        }

       
        public class Response
        {
            public const string SUCCESS = "success";
            public const string FAILURE = "failure";
            public const string ERROR = "error";

        }


        private ReplicationManager rm;
        public IPAddress ipAddr;
        private IPAddress primaryIPAddress;
        private GameMatchmaker _gameMatchmaker;

        // Will use index as number of clients who want to be matched with this amount of other clients
        // then once that index has fullfilled its number we will match those in that index to a game.
        //private List<ConcurrentDictionary<string, Socket>> socketsForGameRequests;

        //private List<ConcurrentQueue<ClientInfo>> clientsWaitingForGame;
        //private List<Socket> sockets;
        private List<ClientInfo> connectedClients;
        public List<string> allPlayerNamesUsed;
        
        // --To be removed--
        public Dictionary<string, List<string>> gameSession;
        // -----------------

        public bool isPrimaryServer = false;

        private TcpListener listener;


        public ServerProgram()
        {
               
            connectedClients = new List<ClientInfo>();
            _gameMatchmaker = new GameMatchmaker();
            //socketsForGameRequests = new List<ConcurrentDictionary<string, Socket>>();
           // clientsWaitingForGame = new List<ConcurrentQueue<ClientInfo>>();

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
            // readServerStatusFromConsole();

            // Initalize a replica and make it listen
            rm = new ReplicationManager(this);

            // Initalize a listening port for replication Manager.
            // TODO: Might need to change the way this code is being called. 
            // new Task(() => { rm.ListenReplica(); }).Start();
        }

        /// <summary>
        /// this method gets information from console about status of server and their ip addresses
        /// </summary>
        //private void readserverstatusfromconsole()
        //{
        //    messages to the console when server starts
        //    console.writeline("server started! this address is: " + ipaddr);

        //    broadcast to local network trying to find primary server


        //    console.writeline("set this to primary? (y/n)");

        //    get user input and for either yes or no and deal with other inputs

        //   string getinput = console.readline().trim().toupper();
        //    while (getinput != "y" && getinput != "n")
        //    {
        //        console.writeline("input is wrong, please indicate if this is the primary server or not by inputing y or n? ");
        //        getinput = console.readline().trim().toupper();
        //    }

        //    check that what the user has input
        //   ipaddress ipaddress = ipaddr;
        //    if (getinput == "y")
        //    {
        //        isprimaryserver = true;
        //        ipaddr = ipaddress;
        //        primaryipaddress = ipaddress;
        //    }
        //    else if (getinput == "n")
        //    {
        //        console.writeline("what is the ip address of the primary server? ");
        //        string getip = console.readline();

        //        while (!ipaddress.tryparse(getip, out ipaddress))
        //        {
        //            console.writeline("there was a mistake in your ip address input. what is the ip address of the primary server in the form x.x.x.x? ");
        //            getip = console.readline();
        //        }
        //        primaryipaddress = ipaddress;
        //    }
        //    code shouldn't hit else part
        //    else
        //    {
        //        console.writeline("error");
        //    }

        //}

        void EstablishConnection(TcpClient tcpclient)
        {
            // Socket s = tcpclient.Client;
            NetworkStream netStream = tcpclient.GetStream();
            //StringBuilder sb = new StringBuilder();
            //sockets.Add(tcpclient.Client);
            ClientInfo aConnectedClient = new ClientInfo(tcpclient);
            connectedClients.Add(aConnectedClient);

            Console.WriteLine("Connection accepted from client " + (tcpclient.Client.RemoteEndPoint as IPEndPoint).Address); //+ ipaddr + " : " + portNumber);

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
            string incomingMessage = Encoding.ASCII.GetString(bytes).Trim();
            incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0")).Trim();

            string requestType;
            string requestMessage;
            MessageParser.ParseNext(incomingMessage, out requestType, out requestMessage);

            Console.WriteLine("REQ: " + requestType + " " + requestMessage);

            string responseMessage = Response.ERROR + " Invalid Request Message";

            if (requestType == Request.GAME)
            {
                // Get playername
                string pName;
                MessageParser.ParseNext(requestMessage, out pName, out requestMessage);

                aConnectedClient.PlayerName = pName;
                // Get number of players the player wants to be matched
                string numberOfPeers;
                MessageParser.ParseNext(requestMessage, out numberOfPeers, out requestMessage);

                if (_gameMatchmaker.IsInQueue(pName) == -1)
                {
                    _gameMatchmaker.AddPlayerToQueue(aConnectedClient, int.Parse(numberOfPeers));
                

                    // Find game match
                    _gameMatchmaker.MatchPeers(this);
                
                    // Legacy functionality: copy GameSessions to Dictionary
                    GameSession[] sessions = _gameMatchmaker.GameSessions;
                    gameSession = new Dictionary<string, List<string>>();
                    for (int i = 1; i <= sessions.Length; i++)
                    {
                        List<string> playerInfos = new List<string>();
                        string[] msgs = sessions[i-1].ToMessage().Split(',');

                        foreach(string m in msgs)
                        {
                            if (m.Trim() != String.Empty) { 
                                playerInfos.Add(m.Trim());
                            }
                        }

                        gameSession.Add(i.ToString(), playerInfos);
                    }
                }
                else
                {
                    responseMessage = Response.FAILURE + " You have already requested a game!";
                    Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                    byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                    netStream.Write(byteToSend, 0, byteToSend.Length);

                    if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                    {
                        connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                        tcpclient.Close();
                    }
                }

            }
            else if (requestType == Request.RECONN)
            {

                string playername,  gameId;

                MessageParser.ParseNext(requestMessage, out playername, out gameId);

                GameSession gSession = _gameMatchmaker.GetGameSession(int.Parse(gameId));
                if (gSession != null && gSession.ContainsPlayer(playername))
                {
                    ClientInfo reconnectedPlayer = gSession.GetPlayer(playername);
                    if(reconnectedPlayer.IPAddr != aConnectedClient.IPAddr)
                    {
                        reconnectedPlayer.IPAddr = aConnectedClient.IPAddr;
                    }


                    responseMessage = Response.SUCCESS + " " + Request.RECONN + " ";
                    responseMessage += gSession.ToMessage();
                }
                else 
                {
                    responseMessage = Response.FAILURE + " " + Request.RECONN + "  No such game exists OR You don't belong in this game";
                }

                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }

            }
            else if (requestType == Request.PLAYERS)
            {


                responseMessage = Response.SUCCESS + " " + Request.PLAYERS + "  " + (connectedClients.Count - _gameMatchmaker.NumOfClientsInQueue);
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }

            }
            else if (requestType == Request.CANCEL)
            {

                string playername;
                
                MessageParser.ParseNext(requestMessage, out playername, out requestMessage);
                int qNum = _gameMatchmaker.IsInQueue(playername);
                if (qNum == -1)
                {
                    responseMessage = Response.FAILURE + " " + Request.CANCEL + " You are not in game queue.";
                }
                else
                {
                    // All the data has been read from the 
                    // client. Display it on the console.
                    ClientInfo playerToCancelRequest = _gameMatchmaker.Queues[qNum].Where(ci => ci.PlayerName == playername).First();
                    TcpClient gameReqClient = playerToCancelRequest.TcpClient;
                    NetworkStream stm = gameReqClient.GetStream();

                    responseMessage = Response.SUCCESS + " " + Request.CANCEL + " YOU CANCELED your match request.";

                    Console.WriteLine("DEBUG: Response sent: " + responseMessage);
                    byte[] b = Encoding.ASCII.GetBytes(responseMessage);
                    stm.Write(b, 0, b.Length);

                    gameReqClient.Close();
                
                }


                // Echo the data back to the client.
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);


                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }

            }
            else if (requestType == Request.CHECKNAME)
            {
                var aPlayerName = requestMessage;
                if (allPlayerNamesUsed.IndexOf(aPlayerName) == -1)
                {
                    responseMessage = Response.SUCCESS + " " + Request.CHECKNAME + " This name is not taken";
                    allPlayerNamesUsed.Add(aPlayerName);
                }
                else
                {
                    responseMessage = Response.FAILURE + " " + Request.CHECKNAME + " This name already exists";
                }

                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client == aConnectedClient))
                {
                    connectedClients.Remove(aConnectedClient);
                    tcpclient.Close();
                }

            } else if (requestType == Request.SERVRECONN)
            {
                responseMessage = Response.SUCCESS + " " + Request.SERVRECONN;
                var aPlayerName = requestMessage;

           
                int qNum  = _gameMatchmaker.IsInQueue(aPlayerName);

                if (qNum != -1)
                {
                    responseMessage += " " + Request.GAME + " " + qNum;
                }

                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client == aConnectedClient))
                {
                    connectedClients.Remove(aConnectedClient);
                    tcpclient.Close();
                }
            } else
            {
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client == aConnectedClient))
                {

                    connectedClients.Remove(aConnectedClient);
                    tcpclient.Close();
                }
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
                    EstablishConnection(tcpclient);
                }).Start();
               
          
            } while (true);
            /* clean up */

          
        }

        public bool TestAndRemoveDisconnectedClients(ClientInfo c)
        {
            byte[] testMsg = new byte[1];
            int timeToTry = 2;
            TcpClient tcpclient = c.TcpClient;
            do
            {
                try
                {
                    tcpclient.Client.Send(testMsg, 0, 0);

                }
                catch (Exception)
                {
                    if (timeToTry <= 0)
                    {
                        if (tcpclient.Client.Connected)
                        {
                            connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                            tcpclient.Close();
                            return false;
                        }
                    }
                }

                break;
            } while (timeToTry > 0);
            return true;
        }

        public List<ClientInfo> ConnectedClients
        {
            get { return connectedClients; }
        }

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
