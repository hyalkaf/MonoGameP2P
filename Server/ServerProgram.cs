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
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Server
{
    public class ServerProgram
    {
        public class Request
        {
            /* Request from client*/
            public const string GAME = "game";
            public const string PLAYERS = "players";
            public const string CANCEL = "cancel";
            public const string CHECKNAME = "checkname";
            public const string RECONN = "reconn";
            public const string SERVRECONN = "servreconn";

            /* Request from in-game peer*/
            public const string RMPLAYER = "rmplayer";
        }

        /// <summary>
        /// Response messages
        /// </summary>
        public class Response
        {
            public const string SUCCESS = "success";
            public const string FAILURE = "failure";
            public const string ERROR = "error";
        }

        // replication manager assoicated with this server
        private ReplicationManager rm;

        // IP of this server
        public IPAddress IPAddr { get; private set; }

        // Game Match Maker responsible for matching peers with games
        private GameMatchmaker _gameMatchmaker;

        // connected clients to server.
        private List<ClientInfo> connectedClients;

        // Player names 
        private ObservableCollection<string> allPlayerNamesUsed;

        // Flag for primary or backup condition of servers
        public bool isPrimaryServer = false;

        // TCP Listener for this server
        private TcpListener listener;

        // Size of buffer for receiving messages
        private static readonly int SIZE_OF_BUFFER = 2048;

        /// <summary>
        /// Constructor for server
        /// </summary>
        public ServerProgram()
        {
            // Initialize fields of class   
            connectedClients = new List<ClientInfo>();
            _gameMatchmaker = new GameMatchmaker();

            // Initialize Event handler for when match maker has changes in it.
            _gameMatchmaker.MatchMakerWasModifiedEvent += new EventHandler((sender, e) => MatchMakerChangedEvent(sender, e, _gameMatchmaker.changedData));

            // Initialize player names and their event handler when they change
            // so that all backups are update using replication manager
            allPlayerNamesUsed = new ObservableCollection<string>();
            allPlayerNamesUsed.CollectionChanged += PlayerNamesChangedEvent;

            /* Initializes the Listener */
            // GET IP address
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
            IPAddr =  IPAddress.Parse(localIP);

            // Initalize replication manager
            rm = new ReplicationManager(this);
        }

        /// <summary>
        /// This method is an event handler that fires whenever there is a change in match maker internal
        /// fields namely Game queue and game sessions
        /// </summary>
        /// <param name="sender">Sender for event</param>
        /// <param name="e">Event parameters</param>
        /// <param name="fieldThatChanged">This field is used to distinguish which field is concerned in game match maker</param>
        private void MatchMakerChangedEvent(object sender, EventArgs e, string fieldThatChanged)
        {
            if (this.isPrimaryServer)
            {
                rm.SendToBackUPsGameState(fieldThatChanged);
            }
        }

        /// <summary>
        /// This method is called every time a change happens in the list of player names.
        /// </summary>
        /// <param name="sender">Sender for event</param>
        /// <param name="e">Event parameters</param>
        private void PlayerNamesChangedEvent(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (this.isPrimaryServer)
            {
                rm.SendToBackUPsGameState(ReplicationManager.REQ_NAMES);
            }
        }

        /// <summary>
        /// Method responsible for receiving and parsing message using a tcpClient
        /// </summary>
        /// <param name="tcpclient">client that receives the message</param>
        private void EstablishConnection(TcpClient tcpclient)
        {
            NetworkStream netStream = tcpclient.GetStream();

            ClientInfo aConnectedClient = new ClientInfo(tcpclient);
            connectedClients.Add(aConnectedClient);

            Console.WriteLine("Connection accepted from client " + aConnectedClient.IPAddr); 

            tcpclient.ReceiveBufferSize = SIZE_OF_BUFFER;
            byte[] bytes = new byte[tcpclient.ReceiveBufferSize];

            try {
                netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);
            }
            catch (Exception)
            {
                connectedClients.Remove(connectedClients.Find(client => client.TcpClient.Equals(tcpclient)));
                tcpclient.Close();
                return;
            }


            string incomingMessage = Encoding.ASCII.GetString(bytes).Trim();
            incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0")).Trim();

            string requestType;
            string requestMessage;
            MessageParser.ParseNext(incomingMessage, out requestType, out requestMessage);

            Console.WriteLine("REQ: " + requestType + " " + requestMessage);

            string responseMessage = Response.ERROR + " ERROR:Invalid Request Message";

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


                    connectedClients.Remove(connectedClients.Find(client => client.TcpClient.Equals(tcpclient)));
                }
                else
                {
                    responseMessage = Response.FAILURE + " " + Request.GAME +" You have already requested a game!";
                    Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                    byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                    netStream.Write(byteToSend, 0, byteToSend.Length);

     
                    connectedClients.Remove(connectedClients.Find(client => client.TcpClient.Equals(tcpclient)));
                    tcpclient.Close();
   
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


                responseMessage = Response.SUCCESS + " " + Request.PLAYERS + "  " + (connectedClients.Count);
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
                    _gameMatchmaker.CancelGameRequest(playername);
                    responseMessage = Response.SUCCESS + " " + Request.CANCEL + " Cancelled.";
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
                    _gameMatchmaker.CancelGameRequest(aPlayerName);
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
            }else if (requestType == Request.RMPLAYER){
                string playername, gameSessionId;

                MessageParser.ParseNext(requestMessage, out playername, out gameSessionId);

                GameSession gs = _gameMatchmaker.GetGameSession(int.Parse(gameSessionId));
                int status = gs.RemovePlayer(playername);

                if (status == 0)
                {
                    // Trigger update
                    MatchMakerChangedEvent(null, null, "session");

                    responseMessage = Response.SUCCESS + " " + Request.RMPLAYER;
                }
                else
                {
                    responseMessage = Response.ERROR + " " + Request.RMPLAYER + " This player was not in the game.";
                }
              

                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                if (connectedClients.Exists(client => client == aConnectedClient))
                {
                    connectedClients.Remove(aConnectedClient);
                    tcpclient.Close();
                }
            }

            else
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
            listener = new TcpListener(IPAddr, 8001);
            listener.Start();

            Console.WriteLine("The server is running at port 8001...");
            Console.WriteLine("The local End point is  :" + listener.LocalEndpoint);
            

            int counter = 0;
            do
            {
                Console.WriteLine("Waiting for a connection {0} .....", ++counter);
                
                TcpClient tcpclient = listener.AcceptTcpClient();

                Thread connectionThread = new Thread(() => {
                    EstablishConnection(tcpclient);
                });
                connectionThread.IsBackground = true;
                connectionThread.Start();
                   


            } while (true);
          
        }

        public bool TestAndDisconnectClients(ClientInfo c)
        {
            byte[] testMsg = new byte[1];
            int timeToTry = 2;
            if (c.TcpClient == null) return true;
            TcpClient tcpclient = c.TcpClient;
            do
            {
                try
                {
                    tcpclient.Client.Send(testMsg, 0, 0);
                    break;
                }
                catch (Exception)
                {
                    timeToTry--;
                    if (timeToTry <= 0)
                    {
                        if (tcpclient.Client.Connected)
                        {
                            if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                            {
                                connectedClients.Remove(connectedClients.Find(client => client.TcpClient == tcpclient));
                            }
                           
                            tcpclient.Close();
                            
                        }
                        return false;
                    }
                }

                
            } while (timeToTry > 0);
            return true;
        }

        /// <summary>
        /// List of connected clients as array
        /// </summary>
        public List<ClientInfo> ConnectedClients
        {
            get { return connectedClients; }
        }

        /// <summary>
        /// Getter for game sessions
        /// </summary>
        public GameSession[] GetGameSession()
        {
            return _gameMatchmaker.GameSessions;
        }

        /// <summary>
        /// Setter for game sessions.
        /// </summary>
        /// <param name="newGameSessions">The updated game session</param>
        public void SetGameSession(GameSession[] newGameSessions)
        {
            // Change game session
            this._gameMatchmaker.GameSessions = newGameSessions;

            // Update all backup servers
            if (isPrimaryServer) rm.SendToBackUPsGameState(ReplicationManager.REQ_GAMESESSIONS);
        }

        /// <summary>
        /// Getter for Player Names
        /// </summary>
        public ObservableCollection<string> GetPlayerNames()
        {
            return allPlayerNamesUsed;
        }

        /// <summary>
        /// Setter for player names.
        /// </summary>
        /// <param name="newPlayerNames">The updated player names</param>
        public void SetPlayerNames(ObservableCollection<string> newPlayerNames)
        {
            allPlayerNamesUsed = newPlayerNames;

            // Update all backup servers
            if (isPrimaryServer) rm.SendToBackUPsGameState(ReplicationManager.REQ_NAMES);
        }

        /// <summary>
        /// Getter for Clients Waiting for games.
        /// </summary>
        public ObservableCollection<ConcurrentQueue<ClientInfo>> GetClientWaitingForGame()
        {
            return _gameMatchmaker.ClientGameQueue;
        }

        /// <summary>
        /// Setter for clients who are waiting for game to be matched.
        /// </summary>
        /// <param name="newClientsWaitingForGame">The new collection to take place</param>
        public void SetClientsWaitingForGame(ObservableCollection<ConcurrentQueue<ClientInfo>> newClientsWaitingForGame)
        {
            // Change game session
            this._gameMatchmaker.ClientGameQueue = newClientsWaitingForGame;

            // Update all backup servers
            if (isPrimaryServer) rm.SendToBackUPsGameState(ReplicationManager.REQ_MATCH);
        }
        
        /// <summary>
        /// Main server method
        /// </summary>
        /// <param name="args">console args that are not being used here</param>
        static void Main(string[] args)
        {
            try
            {
                ServerProgram svr = new ServerProgram();

            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR from server listening.....\n" + e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

    }

}
