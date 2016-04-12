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
            // so that all backups are updated using replication manager
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

            // Initialize replication manager
            rm = new ReplicationManager(this);
        }

        /// <summary>
        /// This method is an event handler that fires whenever there is a change in match maker internal
        /// fields namely Game queue and game sessions
        /// </summary>
        /// <param name="sender">Sender for event</param>
        /// <param name="e">Event parameters</param>
        /// <param name="fieldThatChanged">This field is used to distinguish which field is concerned in game match maker.</param>
        private void MatchMakerChangedEvent(object sender, EventArgs e, string fieldThatChanged)
        {
            if (this.isPrimaryServer)
            {
                // In case this is a primary server then update information in backup servers
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
                // In case this is a primary server then update information in backup servers
                rm.SendToBackUPsGameState(ReplicationManager.REQ_NAMES);
            }
        }

        /// <summary>
        /// Method responsible for receiving and parsing message using a tcpClient
        /// </summary>
        /// <param name="tcpclient">client that receives the message</param>
        private void EstablishConnection(TcpClient tcpclient)
        {
            // Get the stream for sending and receiving data in this tcp client
            NetworkStream netStream = tcpclient.GetStream();

            // add this client to the connected clients
            ClientInfo aConnectedClient = new ClientInfo(tcpclient);
            connectedClients.Add(aConnectedClient);

            // Message to the console
            Console.WriteLine("Connection accepted from client " + aConnectedClient.IPAddr); 

            // set buffer size for receving messages.
            tcpclient.ReceiveBufferSize = SIZE_OF_BUFFER;
            byte[] bytes = new byte[tcpclient.ReceiveBufferSize];

            try {
                // Read incoming requests from clients
                netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);
            }
            catch (Exception)
            {
                // In case of an error then remove the client and close the connection to it.
                connectedClients.Remove(connectedClients.Find(client => client.TcpClient.Equals(tcpclient)));
                tcpclient.Close();
                return;
            }

            // Convert message received to a string
            string incomingMessage = Encoding.ASCII.GetString(bytes).Trim();
            incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0")).Trim();

            // Get request Type and parameters of message that was received.
            string requestType;
            string requestMessage;
            MessageParser.ParseNext(incomingMessage, out requestType, out requestMessage);

            // Message to the console
            Console.WriteLine("REQ: " + requestType + " " + requestMessage);

            // Response message will be appended with the error message first 
            // in case request type are non of the types specified
            string responseMessage = Response.ERROR + " ERROR:Invalid Request Message";

            // If a client is requesting for a game
            if (requestType == Request.GAME)
            {
                // Get playername
                string pName;
                MessageParser.ParseNext(requestMessage, out pName, out requestMessage);

                // Assign that player name to this client.
                aConnectedClient.PlayerName = pName;

                // Get number of players the player wants to be matched with.
                string numberOfPeers;
                MessageParser.ParseNext(requestMessage, out numberOfPeers, out requestMessage);
                
                // check if this player hasn't requeted a game then add it otherwise display error message
                if (_gameMatchmaker.IsInQueue(pName) == -1)
                {
                    // Add player to queue
                    _gameMatchmaker.AddPlayerToQueue(aConnectedClient, int.Parse(numberOfPeers));
                
                    // Find game match
                    _gameMatchmaker.MatchPeers(this);

                    // Remove this player from connected clients to server after requesting a game.
                    connectedClients.Remove(connectedClients.Find(client => client.TcpClient.Equals(tcpclient)));
                }
                else
                {
                    // Message to the console.
                    responseMessage = Response.FAILURE + " " + Request.GAME +" You have already requested a game!";
                    Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                    // Write a response back to the client
                    byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                    netStream.Write(byteToSend, 0, byteToSend.Length);

                    // Remove this player from connected clients to server after requesting a game.
                    // TODO: Ask Gem why need to do this in both cases
                    connectedClients.Remove(connectedClients.Find(client => client.TcpClient.Equals(tcpclient)));
                    tcpclient.Close();
   
                }

            }
            // If client is requesting to reconnect back to a game.
            else if (requestType == Request.RECONN)
            {
                // Extract player name and game id from the request.
                string playername,  gameId;
                MessageParser.ParseNext(requestMessage, out playername, out gameId);

                // Get the game session with the specified game id
                GameSession gSession = _gameMatchmaker.GetGameSession(int.Parse(gameId));

                // If such game exists and player is in that game
                // Then add that player back to the game
                if (gSession != null && gSession.ContainsPlayer(playername))
                {
                    // Change player that reconnected to have the same IP address as the one in the game session
                    ClientInfo reconnectedPlayer = gSession.GetPlayer(playername);
                    if(reconnectedPlayer.IPAddr != aConnectedClient.IPAddr)
                    {
                        reconnectedPlayer.IPAddr = aConnectedClient.IPAddr;
                    }

                    // Add success to the response message since reconnect request did find a player
                    responseMessage = Response.SUCCESS + " " + Request.RECONN + " ";
                    responseMessage += gSession.ToMessage();
                }
                else 
                {
                    // Response message is a failure message
                    responseMessage = Response.FAILURE + " " + Request.RECONN + "  No such game exists OR You don't belong in this game";
                }

                // Write response message to the screen
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                // Write to the channel back to the client
                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                // If this client is in connected clients then remove it since it was matched with a game.
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
            // If client request to cancel his request for a game.
            else if (requestType == Request.CANCEL)
            {
                // Get player name who is trying to cancel its game request
                string playername;
                MessageParser.ParseNext(requestMessage, out playername, out requestMessage);

                // check if player in the queue of game requests so that it's removed
                int qNum = _gameMatchmaker.IsInQueue(playername);
                if (qNum == -1)
                {
                    // In case it's not in the queue then respond with failure
                    responseMessage = Response.FAILURE + " " + Request.CANCEL + " You are not in game queue.";
                }
                else
                {
                    // In case it's in the game queue then cancel its game request.
                    _gameMatchmaker.CancelGameRequest(playername);

                    // Respond with a success message after canceling
                    responseMessage = Response.SUCCESS + " " + Request.CANCEL + " Cancelled.";
                }

                // Echo the data back to the client.
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                // Write response back to the client
                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                // Check that this client is removed from connected clients after canceling its request.
                if (connectedClients.Exists(client => client.TcpClient == tcpclient))
                {
                    connectedClients.Remove(connectedClients.Where(client => client.TcpClient == tcpclient).First());
                    tcpclient.Close();
                }

            }
            // if request for client is for a new name
            else if (requestType == Request.CHECKNAME)
            {
                // Check that name exists or not and respond with messages accrodingly
                var aPlayerName = requestMessage;
                if (allPlayerNamesUsed.IndexOf(aPlayerName) == -1)
                {
                    // Success in case name doesn't exist and add it to player names
                    responseMessage = Response.SUCCESS + " " + Request.CHECKNAME + " This name is not taken";
                    allPlayerNamesUsed.Add(aPlayerName);
                }
                else
                {
                    // Failure in case name exist in the list of player names
                    responseMessage = Response.FAILURE + " " + Request.CHECKNAME + " This name already exists";
                }

                // Console message
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                // Write back to the client with response message
                byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                netStream.Write(byteToSend, 0, byteToSend.Length);

                // Remove this client from list of connected clients.
                if (connectedClients.Exists(client => client == aConnectedClient))
                {
                    connectedClients.Remove(aConnectedClient);
                    tcpclient.Close();
                }

            }
            // Request is of type 
            else if (requestType == Request.SERVRECONN)
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
            }
            // 
            else if (requestType == Request.RMPLAYER){
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
