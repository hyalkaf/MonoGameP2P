using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedCode;

namespace Server
{
    /// <summary>
    /// This class will be aggragted in every server program. It will be communicating with other
    /// backup replication managers for other servers. It will take care of replicating data 
    /// from its assoicated server.
    /// </summary>
    public class ReplicationManager
    {
        // List of all ip addresses of backup servers currently existing
        // Zero position will have the primary server IP address.
        // and sequentially the list of backups in positions 1, 2 and so on 
        // for backup servers that should take position of primary.
        public List<IPAddress> serversAddresses { get; set; }


        // This timer will be running every 5 seconds to check the primary server's existence
        // This will be used when primary server is died or out of connection.
        private static readonly int CHECK_MESSAGE_INTERVAL = 5000;
        private Timer timerForCheckingPrimaryExistence;

        // lock object for callback method that checks primary existence.
        // this will prevent multiple threads from queueing the callback.
        private Object checkPrimaryCallbackLock = new Object();

        // backupWasUpdated is needed to prevent the case when a backup replication manager
        // receives an update for a new elected primary while it has queued callbacks for 
        // check messages making it become the primary in that case. this will happen when replication
        // managers have almost the exact starting time for the timer that checks primary existence.
        bool backupWasUpdated = false;

        // Request and response messsages between backup servers and primary server
        // These are mentioned in our design document
        // REQ_BACKUP is a request message that will be sent whenever a new backup is initialized
        // It will be sending this request message with its own IP address to the primary.
        private static readonly string REQ_BACKUP = "backup";
        // REQ_ADDRESSES is a request message from primary servers to backup servers 
        // sending them information about addresses of all backup server currently in 
        // pool of servers. This will be sent after a new backup server have entered the pool
        // and have already sent its own ip address.
        private static readonly string RES_ADDRESSES = "address";
        // REQ_NAMES is a request message that will be sent from backup to primary.
        // It will not contain any information. 
        private static readonly string REQ_NAMES = "nameRequest";
        // RES_NAMES is a response message that will be sent from primary server to all 
        // back ups
        // RES_NAMES is a response message acknowledging whoever sent the request "name" that
        // it was received correctly.
        private static readonly string RES_NAMES = "playerNames";
        // REQ_GAMESESSIONS is a request and response message. As a request from backup to server it will not 
        // contain any information. As a response, it will the information stored in the game session
        // like sessionID, and players (player name, ID, Port and IP) that are currently
        // playing a game with that unique sessionID.
        private static readonly string REQ_GAMESESSIONS = "sessionRequest";
        private static readonly string RES_GAMESESSIONS = "gameSessions";
        // REQ_CHECK is a request message from backup replication manager to the primary server 
        // checking if it still exists and it can't receive and respond to messages.
        private static readonly string REQ_CHECK = "check";
        // REQ_MATCH is a request and a response message. As a request from backup to server it will not 
        // contain any information. As a response, it will contain information about the queued players
        // waiting to be assigned and matched to other players. This information is game capacity or number
        // of matched clients, and players info in that specific game capacity which is player name, player ID
        // port and IP address.
        public static readonly string REQ_MATCH = "matchesRequest";
        public static readonly string RES_MATCH = "matchesResponse";
        // REQ_UPDATE_BACKUP is a request that will be sent after a new primary is elected.
        // This request holds the new information about the backup servers currently existing
        // in the local network. 
        private static readonly string REQ_UPDATE_BACKUP = "update-backup";
        // TODO: Add responses
        //private static readonly string RESP_SUCCESS = "success";
        // Requests that will be sent from backup to primary server every time 
        // a new backup is initalized. 
        private static readonly string[] MESSAGES_SENT_AND_RECEIEVED_BY_A_NEW_BACKUP = { REQ_BACKUP, REQ_NAMES, REQ_GAMESESSIONS, REQ_MATCH };
        // SIZE_OF_BUGGER is used for initalizing byte array of this size to receive information
        // through the network.
        private static readonly int SIZE_OF_BUFFER = 4096;

        // This is the default port used for broadcasting and listening to 
        // UDP messages. Make sure that firewall is not blocking it as well as
        // Cybera can receive and listen on it.
        private static readonly int PORT_NUMBER_FOR_BROADCASTING_UDP = 15000;

        // Server program assoicated with this replication manager
        public ServerProgram thisServer;

        TCPMessageHandler tcpClientMessageHandler;

        Thread tcpBackupListenThread;

        /// <summary>
        /// Main constructor for initalization of the replication manager. It will
        /// initialize listeners (UDP for broadcast from other replication managers, TCP for
        /// replication manager lisening and sending to other backups). It will also look for primary.
        /// </summary>
        /// <param name="associatedServer">The associated server that initialized this replication manager.</param>
        public ReplicationManager(ServerProgram associatedServer)
        {
            serversAddresses = new List<IPAddress>();
            // assoicate the server with this replication manager
            thisServer = associatedServer;

            tcpClientMessageHandler = new TCPMessageHandler();

            // Run listening to other backups in it's own thread
            tcpBackupListenThread = new Thread(() =>
            {
                ListenReplica();
            });

            // Start thread lisening for other backup replication managers TCP messages
            tcpBackupListenThread.Start();

            // Start lisening and broadcasting for UDP channel as well.
            BroadcastForReplication replicationManagerUDP = new BroadcastForReplication(true, PORT_NUMBER_FOR_BROADCASTING_UDP, this);            
  
        }

        /// <summary>
        /// This method is used to initialize replication depedning on whether it's a server.
        /// </summary>
        /// <param name="isServerPrimary">A bool for whether server is primary or not.</param>
        public void InitializeReplicationManager(bool isServerPrimary, IPAddress primaryServerIP)
        {
            if (!isServerPrimary)
            {
                //if (!serversAddresses.Exists(e => e.Equals(primaryServerIP)))
                //{ 
                    // Add Primary server ip address to replica
                    serversAddresses.Add(primaryServerIP);

                    // Timer for checking if primary is there
                    timerForCheckingPrimaryExistence = new Timer(CheckServerExistence, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

                    // secondary replica sends a replica request
                    DecideOnMessagesSendFromBackUpToServer(true);
                //}
                
            }
            else
            {
                // Add primary server to list of servers
                serversAddresses.Add(thisServer.IPAddr);

                // Make this server start listening
                thisServer.StartListen();
            }
        }

        /// <summary>
        /// This method is responsible for listening to clients(other replication managers)
        /// for their requests.
        /// </summary>
        /// <param name="backupClient">Client sending messages to this replication manager</param>
        public void EstablishConnection(TcpClient backupClient)
        {
            Console.WriteLine("Establishing Connection with {0} {1}", (backupClient.Client.LocalEndPoint as IPEndPoint).Address, (backupClient.Client.RemoteEndPoint as IPEndPoint).Address);

            // TODO: Implement try and catch
            
            string requestMessage = string.Empty;
            try
            {
                // TODO: deal with exceptions in this
                requestMessage = tcpClientMessageHandler.RecieveMessage(backupClient);
            }
            catch(Exception e)
            {
                Console.WriteLine("Failed");
            }

            // Depending on whether server is primary or backup parse messages accrodingly
            if (thisServer.isPrimaryServer)
            {
                // Get appeopraite response
                string responseMessage = parseRequestMessageForPrimary(requestMessage);

                if (requestMessage.StartsWith(REQ_CHECK))
                {
                    try
                    {
                        tcpClientMessageHandler.SendMessage(responseMessage, backupClient);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Failed");
                    }
                    //backupClient.Client.Send(responseMessage);

                    //backupClient.Close();

                    
                }
                else
                {
                    // sendback
                    try
                    {
                        backupClient.GetStream().Write(new byte[1], 0, 0);
                        backupClient.Close();
                    }
                    catch
                    {
                        Console.WriteLine("Failed");
                    }

                    // Accumlate backup indexes from the list of backup ips in case they are died
                    // TODO: refactor this into a method?
                    // TODO: Do I need to remove backup non responsive? since I do check? 
                    // also I have to update others if i find unresponsive ones. What about the fact that I send to everybody too? 
                    // Just use check i guess
                    List<int> indexOfDeadBackupServers = new List<int>();

                    bool keepSending = true; 

                    while (keepSending)
                    {
                        keepSending = false;
                        // Send all backups updated info
                        for (int j = 1; j < serversAddresses.Count; j++)
                        {
                            // 
                            IPAddress backupIP = serversAddresses[j];

                            try
                            {
                                string responseOfBackUpToServerResponseStr = SendMessage(backupIP, 8000, responseMessage);

                                // TODO: Test response again.
                            }
                            catch (SocketException)
                            {
                                // Remove dead backups
                                indexOfDeadBackupServers.Add(j);
                            }
                        }

                        // Remove all dead backups if there is any
                        // TODO: Change this to send instead of Ping
                        foreach (int deadBackupind in indexOfDeadBackupServers)
                        {
                            // Ping each backup again 
                            Ping pingBackups = new Ping();
                            PingReply reply = pingBackups.Send(serversAddresses[deadBackupind]);
                            if (!reply.Status.Equals(IPStatus.Success))
                            {
                                serversAddresses.RemoveAt(deadBackupind);
                                keepSending = true;
                            }

                        }
                    }
                }
                    
            }
            // Server is not primary
            else
            {
                //Console.WriteLine("Received messages from primary of this type {0}", requestMessage);

                // Update information for backup 
                // Here we are parsing request but since method is same use same
                // TODO: Update names
                if (requestMessage.StartsWith(REQ_UPDATE_BACKUP))
                {
                    backupWasUpdated = true;
                }
                parseResponseMessageForBackup(requestMessage);

                // TODO: Response of success

                // TODO: how does socket differ from tcp client.
            }
            backupClient.Close();


        }
            
           
        /// <summary>
        /// This method takes a request that was sent through the network from backup to primary and parses it 
        /// and return a response to it.
        /// </summary>
        /// <param name="requestMessage">This parameter contains what was sent through the network.</param>
        /// <returns>response message in bytes array.</returns>
        private string parseRequestMessageForPrimary(string requestMessage)
        {
            string requestType;
            string messageParam;
            string responseMessage = string.Empty;

            // get requestType out of the request message
            if (requestMessage.IndexOf(" ") == -1)
            {
                requestType = requestMessage;
            }
            else
            {
                requestType = requestMessage.Substring(0, requestMessage.IndexOf(" ")).Trim();
            }

            // Append all other parameters at the end of request message to a new variable
            messageParam = requestMessage.Substring(requestType.Length).Trim();

            // Incoming message "replica" from one of the backup servers to primary server.
            if (requestType == REQ_BACKUP)
            {
                // get IP Address of the backup server from message parameters
                string ipAddressString = messageParam;

                // Convert IP address from string to IPAddress
                IPAddress ipAddr;
                if (!IPAddress.TryParse(ipAddressString, out ipAddr))
                {
                    // In case what was sent can't be parsed as an IP address
                    // TODO: deal with this error in some way
                    Console.WriteLine("ERROR");
                }
                else
                {
                    // add information about this replica
                    //Console.WriteLine("Add Replica IP {0} to Server {1}", ipAddr, primaryServerIp);

                    // Add backup ip address to primary server list
                    serversAddresses.Add(ipAddr);

                    // Create a response back to the replicationManager of the backup server
                    // add required information to be sent back
                    responseMessage = RES_ADDRESSES + " ";

                    // Send backup servers ip addresses starting from first backup server exculding primary server
                    for (int i = 0; i < serversAddresses.Count; i++)
                    {
                        // Comma shouldn't be added at the end of the message
                        if (i != serversAddresses.Count - 1)
                        {
                            responseMessage += serversAddresses[i] + ",";
                        }
                        else
                        {
                            responseMessage += serversAddresses[i];
                        }

                    }

                    responseMessage += "\n\n";
                }
            }
            else if (requestType == REQ_NAMES || requestType == REQ_GAMESESSIONS || requestType == REQ_MATCH)
            {
                responseMessage = ConstructPrimaryMessageToBackupBasedOnRequestType(requestType);

                responseMessage += "\n\n";
            }

            return responseMessage;
        }

        /// <summary>
        /// This method is used to parse reponse messages after sending requests. 
        /// </summary>
        /// <param name="reposnseMessage">Response messages</param>
        private void parseResponseMessageForBackup(string reposnseMessage)
        {
            string responseType = string.Empty;
            string messageParam = string.Empty;

            // get requestType out of the request message
            if (reposnseMessage.IndexOf(" ") == -1)
            {
                responseType = reposnseMessage;
            }
            else
            {
                responseType = reposnseMessage.Substring(0, reposnseMessage.IndexOf(" ")).Trim();
            }

            // Append all other parameters at the end of request message to a new variable
            messageParam = reposnseMessage.Substring(responseType.Length).Trim();


            if (responseType.Equals(RES_ADDRESSES) || responseType.Equals(REQ_UPDATE_BACKUP))
            {
                // get IP Addresses of all the other programs 
                string[] arrayOfIPAddresses = messageParam.Split(',');
                List<IPAddress> allReplicaAddrTemp = new List<IPAddress>();

                // Convert IP address from string to IPAddress
                foreach (string tempIP in arrayOfIPAddresses)
                {
                    IPAddress ipAddr;
                    if (!IPAddress.TryParse(tempIP, out ipAddr))
                    {
                        // In case what was sent can't be parsed as an IP address
                        // TODO: deal with this error in some way
                        Console.WriteLine("ERROR");
                    }
                    else
                    {
                        allReplicaAddrTemp.Add(ipAddr);
                    }
                }

                //
                serversAddresses = allReplicaAddrTemp;

            }
            else if (responseType == REQ_NAMES || responseType == REQ_GAMESESSIONS || responseType == REQ_MATCH)
            {
                if (!string.IsNullOrEmpty(messageParam) || responseType.Equals(REQ_MATCH))
                {
                    ParseServerResponseMessageToBackUpForGameInfo(responseType, messageParam);
                }
                
            }

        }




        private string ConstructPrimaryMessageToBackupBasedOnRequestType(string requestType)
        {
            // Add response Type
            string responseMessage = string.Empty;
            ObservableCollection<string> names = thisServer.GetPlayerNames();
            GameSession[] sessions = thisServer.GetGameSession();
            ObservableCollection<ConcurrentQueue<ClientInfo>> clientsWaitingForgame = thisServer.GetClientWaitingForGame();

            // based on request get the server info needed
            /*if (requestType.Equals(RES_ADDRESSES))
            {
                Console.WriteLine("ERROR in ConstructPrimaryMessageToBackupBasedOnRequestType");
            }*/
            if (requestType.Equals(REQ_NAMES))
            {
                responseMessage = ConstructPrimaryMessageNames(names);
            }
            else if (requestType.Equals(REQ_GAMESESSIONS))
            {
                responseMessage = ConstructPrimaryMessageSession(sessions);
            }
            else if (requestType.Equals(REQ_MATCH))
            {
                responseMessage = ConstructPrimaryMessageMatch(clientsWaitingForgame);
            }
            else if (requestType.Equals(REQ_UPDATE_BACKUP))
            {
                responseMessage = ConstructPrimaryMessageUpdateAddresses(requestType);
            }
            
            return responseMessage;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string ConstructPrimaryMessageUpdateAddresses(string updateBackupsRequestMessage)
        {

            // Debug
            Console.WriteLine("Updating addresses for all backups");

            // Create a response back to the replicationManager of the backup server
            // add required information to be sent back
            string requestMessage = updateBackupsRequestMessage + " ";

            // Send backup servers ip addresses starting from first backup server exculding primary server
            for (int i = 0; i < serversAddresses.Count; i++)
            {
                // Comma shouldn't be added at the end of the message
                if (i != serversAddresses.Count - 1)
                {
                    requestMessage += serversAddresses[i] + ",";
                }
                else
                {
                    requestMessage += serversAddresses[i] ;
                }

            }

            return requestMessage;
            
        }

        /// <summary>
        /// This method constructs a message that will be sent from primary to replica for name request.
        /// </summary>
        /// <param name="names">List of names of players to be sent from primary server to replica.</param>
        /// <returns>Message to be sent to the replica</returns>
        private string ConstructPrimaryMessageNames(ObservableCollection<string> names)
        {
            string responseMessage = RES_NAMES + " ";

            // send client names on the server
            for (int i = 0; i < names.Count; i++)
            {
                // Comma shouldn't be added at the end of the message
                if (i != names.Count - 1)
                {
                    responseMessage += names[i] + ",";
                }
                else
                {
                    responseMessage += names[i];
                }
            }

            return responseMessage;
        }

        /// <summary>
        /// This method constructs a message that will be sent from primary to replica for session request.
        /// </summary>
        /// <param gameSessionInfo="gameSessionInfo">GameSession Object of gameIDs to list of player info to be sent from primary server to replica.</param>
        /// <returns>Message to be sent to the replica</returns>
        public string ConstructPrimaryMessageSession(GameSession[] gameSessions)
        {
            string responseMessage = "session" + " ";
            int counter = 0;

            // send client names on the server
            foreach (GameSession gameSession in gameSessions)
            {
                responseMessage += gameSession.ID + " ";


                // append all players info to response message
                for (int j = 0; j < gameSession.Players.Count(); j++)
                {
                    if (j != gameSession.Players.Count() - 1)
                    {
                        responseMessage += gameSession.Players[j].ToMessage() + ",";
                    }
                    else
                    {
                        responseMessage += gameSession.Players[j].ToMessage();
                    }
                }

                // Append a newline at the end
                if (counter != gameSessions.Count() - 1)
                {
                    responseMessage += "\n";
                }

                counter++;
            }

            // Append new lines for end of message
            responseMessage += "\n\n";

            return responseMessage;
        }


        private string ConstructPrimaryMessageMatch(ObservableCollection<ConcurrentQueue<ClientInfo>> clientsWaitingForGame)
        {
            string responseMessage = REQ_MATCH + " ";

            // send client names on the server
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                for (int j = 0; j < clientsWaitingForGame[i].Count; j++)
                {

                    if (j.Equals(0))
                    {
                        responseMessage += i + " ";
                    }

                    responseMessage += clientsWaitingForGame[i].ElementAt(j).ToMessage();

                    if (j.Equals(clientsWaitingForGame[i].Count - 1))
                    {
                        responseMessage += ",";
                    }
                    else
                    {
                        responseMessage += " ";
                    }
                }

            }

            return responseMessage;
        }

        /// <summary>
        /// This method constructs a message that will be sent from primary to replica for name request.
        /// </summary>
        /// <param name="names">List of names of players to be sent from primary server to replica.</param>
        /// <returns>Message to be sent to the replica</returns>
        public void ParseServerResponseMessageToBackUpForGameInfo(string responseType, string messageParam)
        {
            // If 
            if (responseType == REQ_NAMES)
            {
                // get player names
                string[] arrayOfPlayerNames = messageParam.Split(',');

                // Add player names to a temp variable
                ObservableCollection<string> tempPlayerNames = new ObservableCollection<string>();

                // Convert IP address from string to IPAddress
                foreach (string tempName in arrayOfPlayerNames)
                {
                    tempPlayerNames.Add(tempName);
                }

                // Set playerNames
                thisServer.SetPlayerNames(tempPlayerNames);

            }

            else if (responseType == REQ_GAMESESSIONS)
            {
                // split games sessions
                string[] arrayOfSessions = messageParam.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Temp variable for game session
                List<GameSession> tempGameSession = new List<GameSession>();

                // Convert IP address from string to IPAddress
                foreach (string tempSession in arrayOfSessions)
                {
                    // Split each game session by comma serperator
                    string[] arrayOfSpecificSession = tempSession.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    GameSession gameSession = null;
                    List<ClientInfo> players = new List<ClientInfo>();
                    string gameID = "";

                    // Use an integer to differ between string with gameID and without
                    // First info will contain a game ID
                    int extraIndexForGameID = 1;

                    // Extract Game ID and players Info
                    for (int gameSessionAndPlayerInfoIndex = 0; gameSessionAndPlayerInfoIndex < arrayOfSpecificSession.Count(); gameSessionAndPlayerInfoIndex++)
                    {
                        // Split speicific info by spaces
                        string[] arrayOfGameSessionAndPlayerSpecificInfo = arrayOfSpecificSession[gameSessionAndPlayerInfoIndex].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries); ;

                        // TODO: Add TCP client
                        ClientInfo player;
                        if (extraIndexForGameID == 1)
                        {
                            gameID = arrayOfGameSessionAndPlayerSpecificInfo[0];
                            // TODO: TryParse
                            gameSession = new GameSession(int.Parse(gameID));
                        }

                        // Check that the gameSession doesn't alreay exist on this backup server
                        // if (!thisServer.GetGameSession().All(gameSessionparam => gameSessionparam.ID.Equals(gameID)))
                        //{
                        // TODO: TryPArse
                        player = new ClientInfo(IPAddress.Parse(arrayOfGameSessionAndPlayerSpecificInfo[0 + extraIndexForGameID]),
                            int.Parse(arrayOfGameSessionAndPlayerSpecificInfo[1 + extraIndexForGameID]), 
                            arrayOfGameSessionAndPlayerSpecificInfo[2 + extraIndexForGameID], 
                            int.Parse(arrayOfGameSessionAndPlayerSpecificInfo[3 + extraIndexForGameID]));
                        //}


                        // After extracting gameID, index goes back to zero.
                        extraIndexForGameID = 0;

                        players.Add(player);

                        
                    }

                    // After extracting gameID, index goes back to zero.
                    extraIndexForGameID = 1;


                    // Add to the gamesession
                    gameSession.SetPlayers = players;

                    // Add game session
                    tempGameSession.Add(gameSession);
                }

                // Add to game session of server
                thisServer.SetGameSession(tempGameSession.ToArray());

            }
            else if (responseType.Equals(REQ_MATCH))
            {
                // Split game queue by delimiter comma
                string[] arrayOfGameQueues = messageParam.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                ObservableCollection<ConcurrentQueue<ClientInfo>> tempQueues = new ObservableCollection<ConcurrentQueue<ClientInfo>>();

                int gameCapacity = -1;

                // Use an integer to differ between string with gameID and without
                // First info will contain a game ID
                int extraIndexForGameCapacity = 1;

                foreach (string gameQueue in arrayOfGameQueues)
                {
                    

                    // Extract index of gameQueue
                    string[] arrayOfGameQueue = gameQueue.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                    if (extraIndexForGameCapacity == 1)
                    {
                        gameCapacity = int.Parse(arrayOfGameQueue[0]);
                        // TODO: TryParse
                        for (int i = tempQueues.Count; i <= gameCapacity; i++)
                        {
                            tempQueues.Add(new ConcurrentQueue<ClientInfo>());
                        }
                    }

                    for (int i = 1; i < arrayOfGameQueue.Count(); i += 4)
                    {
                        ClientInfo player;
                        // For every four enteries get the relvent info
                        player = new ClientInfo(IPAddress.Parse(arrayOfGameQueue[i]),
                            int.Parse(arrayOfGameQueue[i + 1]),
                            arrayOfGameQueue[i + 2],
                            int.Parse(arrayOfGameQueue[i + 3]));

                        tempQueues[gameCapacity].Enqueue(player);
                    }


                    // After extracting gameID, index goes back to zero.
                    extraIndexForGameCapacity = 1;

                }

                thisServer.SetClientsWaitingForGame(tempQueues);

                // Debug
                for (int i = 0; i < thisServer.GetClientWaitingForGame().Count; i++)
                {
                    foreach (ClientInfo cli in thisServer.GetClientWaitingForGame()[i])
                    {
                        Console.WriteLine("backup received match of queue number {0} and player address {1} and port {2} and id {3} and playername {4} ", i, cli.IPAddr, cli.ListeningPort, cli.PlayerId, cli.PlayerName);
                    }
                }
            }
        }

        /// <summary>
        /// This method chooses between either sending initial messages from backup to primary or 
        /// check message for server existence.
        /// </summary>
        /// <param name="tempMsg">what replicas are trying to send as clients</param>
        public void DecideOnMessagesSendFromBackUpToServer(bool OnInitialize)
        {

            if (OnInitialize)
            {

                // Catch errors 
                try
                {
                    // Loop through three requests for duplicating data
                    for (int i = 0; i < 4; i++)
                    {
                        SendFromReplicaToServerAndParseResponse(MESSAGES_SENT_AND_RECEIEVED_BY_A_NEW_BACKUP[i]);
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("sending from replica to primary for replica, name, and session is not working {0}", e.Message);
                }
            }
            else
            {
                try
                {
                    SendFromReplicaToServerAndParseResponse("check");
                    backupWasUpdated = false;
                }
                catch (SocketException)
                {
                    // In this case: server must have crashed
                    // take over and become the primary 
                    // TODO: This won't work for multiple servers
                    if (!backupWasUpdated && serversAddresses[1].Equals(thisServer.IPAddr))
                    {
                        MakeThisServerPrimary();
                    }
                }
            }
  
        }

        /// <summary>
        /// This method build up message to be sent from each replica to the server.
        /// </summary>
        /// <param name="replicaMsg">request that should be sent to the primary server</param>
        /// <returns>Complete message that should be sent to the primary server</returns>
        private string ConstructReplicaMessagesFromReplicaToServer(string replicaMsg)
        {
            string messageToBeSent = string.Empty;

            // Check type of Message 
            if (replicaMsg.StartsWith(REQ_NAMES))
            {
                // add required information to be sent back
                messageToBeSent = REQ_NAMES + " "; 
            }
            else if (replicaMsg.StartsWith(REQ_BACKUP))
            {
                // Message to be sent 
                messageToBeSent = REQ_BACKUP + " " + thisServer.IPAddr;
            }
            else if (replicaMsg.StartsWith(REQ_CHECK))
            {
                // Message to be sent 
                messageToBeSent = REQ_CHECK + " ";
            }
            else if (replicaMsg.StartsWith(REQ_GAMESESSIONS))
            {
                messageToBeSent = REQ_GAMESESSIONS + " ";
            }
            else if (replicaMsg.StartsWith(REQ_MATCH))
            {
                messageToBeSent = REQ_MATCH + " ";
            }

            return messageToBeSent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tempMsg"></param>
        private void SendFromReplicaToServerAndParseResponse(string tempMsg)
        {
            string messageToBeSent = ConstructReplicaMessagesFromReplicaToServer(tempMsg);

            // replica TCP Client for sending requests to primary server
            // Initalize a new TcpClient
            try
            {
                string responseMessage = SendMessage(serversAddresses[0], 8000, messageToBeSent);
                // Prepare another response to backups
                parseResponseMessageForBackup(responseMessage);
            }
            catch
            {

            }
            
        }

        /// <summary>
        /// This method get triggered whenever a change in the game state or the list of backup servers happen
        /// </summary>
        /// <param name="tempMsg"></param>
        public void SendFromServerToBackUPSWhenStateChanges(string updateType)
        {
            // Construct a message to be sent based on type of update
            string messageUpdate = ConstructPrimaryMessageToBackupBasedOnRequestType(updateType);

            // Send back to all backups the new updated information
            // TODO: We know primary only sends so remove zeroth position
            // Accumlate backup indexes from the list of backup ips in case they are died
            List<int> indexOfDeadBackupServers = new List<int>();
            bool keepSending = true;

            while (keepSending)
            {
                keepSending = false;
                // Send all backups updated info
                for (int j = 1; j < serversAddresses.Count; j++)
                {
                    // 
                    IPAddress backupIP = serversAddresses[j];

                    try
                    {
                        string responseOfBackUpToServerResponseStr = SendMessage(backupIP, 8000, messageUpdate);

                        // TODO: Test response again.
                    }
                    catch (SocketException)
                    {
                        // Remove dead backups
                        indexOfDeadBackupServers.Add(j);
                    }
                }

                // Remove all dead backups if there is any
                // TODO: Change this to send instead of Ping
                foreach (int deadBackupind in indexOfDeadBackupServers)
                {
                    // Ping each backup again 
                    Ping pingBackups = new Ping();
                    PingReply reply = pingBackups.Send(serversAddresses[deadBackupind]);
                    if (!reply.Status.Equals(IPStatus.Success))
                    {
                        serversAddresses.RemoveAt(deadBackupind);
                        keepSending = true;
                    }

                }
            }
        
        }


        private string SendMessage(IPAddress ip, int portNumber, string message)
        {
            string responseOfBackUpToServerResponseStr = string.Empty;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    tcpClient.Connect(ip, portNumber);

                    TCPMessageHandler tcpMessagehandler = new TCPMessageHandler();

                    responseOfBackUpToServerResponseStr = tcpMessagehandler.SendMessage(message, tcpClient);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Failed");
            }

            return responseOfBackUpToServerResponseStr;
        }

        /// <summary>
        /// This will be used to listen on incoming requests from the server.
        /// </summary>
        public void ListenReplica()
        {
            TcpListener rmListener = new TcpListener(thisServer.IPAddr, 8000) ;
            rmListener.Start();
            while (true)
            {
                Console.WriteLine("Listening");
                //Socket sock = rmListener.AcceptSocket();
                TcpClient backupClient = rmListener.AcceptTcpClient();
                
                new Thread(() =>
                {
                    EstablishConnection(backupClient);
                }).Start();
                
            }
        }

        public void MakeThisServerPrimary()
        {
            // Update addresses
            serversAddresses.RemoveAt(0);

            thisServer.isPrimaryServer = true;
            // TODO: change this to try Parse
            // primaryServerIp = IPAddress.Parse("162.246.157.120");
            
            timerForCheckingPrimaryExistence.Change(Timeout.Infinite, Timeout.Infinite);

            // Update backup servers 
            SendFromServerToBackUPSWhenStateChanges(REQ_UPDATE_BACKUP);

            thisServer.StartListen();
        }

        /// <summary>
        /// This method checks that the primary server exists at all time.
        /// </summary>
        /// <param name="state">This parameter has to be passed even though we don't need it here.</param>
        private void CheckServerExistence(object state)
        {
            // Send to primary a message
            lock (checkPrimaryCallbackLock)
            {
                DecideOnMessagesSendFromBackUpToServer(false);
            }
        }

    }
}
