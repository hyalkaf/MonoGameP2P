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


        // This timer will be running every 1 seconds to check the primary server's existence
        // This will be used when primary server is died or out of connection.
        private static readonly int CHECK_MESSAGE_INTERVAL_IN_SECONDS = 1;
        private Timer timerForCheckingPrimaryExistence;

        // This timer will be running every 1 seconds to check the backups existence
        // This will be used when primary server is died or out of connection.
        private Timer timerForCheckingReplicasExistence;

        // lock object for callback method that checks primary existence.
        // this will prevent multiple threads from queueing the callback.
        private Object checkPrimaryCallbackLock = new Object();
        private Object checkBackupCallbackLock = new Object();

        // backupWasUpdated is needed to prevent the case when a backup replication manager
        // receives an update for a new elected primary while it has queued callbacks for 
        // check messages making it become the primary in that case. this will happen when replication
        // managers have almost the exact starting time for the timer that checks primary existence.
        bool backupWasUpdated = false;

        // Request and response messsages between backup servers and primary server
        // These are mentioned in our design document
        // REQ_BACKUP is a request message that will be sent whenever a new backup is initialized
        // It will be sending this request message with its own IP address to the primary.
        public static readonly string REQ_BACKUP = "backup";
        // REQ_ADDRESSES is a response message from primary servers to backup servers 
        // sending them information about addresses of all backup server currently in 
        // pool of servers. This will be sent after a new backup server have entered the pool
        // and have already sent its own ip address with a REQ_BACK.
        public static readonly string RES_ADDRESSES = "address";
        // REQ_NAMES is a request message that will be sent from backup to primary.
        // Asking for names 
        public static readonly string REQ_NAMES = "nameRequest";
        // RES_NAMES is a response message with player names for REQ_NAMES
        public static readonly string RES_NAMES = "playerNames";
        // REQ_GAMESESSIONS is a request message for game session and 
        // RES_GAMESESSIONS is a response message for game sessions information
        public static readonly string REQ_GAMESESSIONS = "sessionRequest";
        public static readonly string RES_GAMESESSIONS = "gameSessions";
        // REQ_CHECK is a request message from backup replication manager to the primary server 
        // checking if it still exists and it can't receive and respond to messages.
        public static readonly string REQ_CHECK = "check";
        // REQ_MATCH is a request message to server asking them  for game queue
        // RES_MATCH is a response message with game queue info
        public static readonly string REQ_MATCH = "matchesRequest";
        public static readonly string RES_MATCH = "matchesResponse";
        // REQ_UPDATE_BACKUP is a request that will be sent after a new primary is elected.
        // This request holds the new information about the backup servers currently existing
        // in the local network. 
        public static readonly string REQ_UPDATE_BACKUP = "update-backup";
        // Requests that will be sent from backup to primary server every time 
        // a new backup is initalized. 
        private static readonly string[] MESSAGES_SENT_AND_RECEIEVED_BY_A_NEW_BACKUP = { REQ_BACKUP, REQ_NAMES, REQ_GAMESESSIONS, REQ_MATCH };

        // This is the default port used for broadcasting and listening to 
        // UDP messages. Make sure that firewall is not blocking it as well as
        // Cybera can receive and listen on it.
        private static readonly int PORT_NUMBER_FOR_BROADCASTING_UDP = 15000;
        // Global variables used in this class
        private static readonly int NEXT_BACKUP_INDEX = 1;
        private static readonly int PRIMARY_INDEX = 0;
        private static readonly int PORT_NUMBER_FOR_LISTENING_AND_SENDING = 8000;
        private static readonly int SEND_TO_DEAD_BACKUPS_FOR = 2;
        private static readonly string REQUESTS_AND_RESPONSES_SUFFIX = "\n\n";
        private static readonly string SEPERATOR_BETWEEN_WORDS = " ";
        private static readonly string SEPERATOR_BETWEEN_PLAYERS = ",";
        private static readonly string SEPERATOR_BETWEEN_SECTIONS_OF_MESSAGES = "\n";

        // Server program assoicated with this replication manager
        public ServerProgram thisServer;

        /// <summary>
        /// Main constructor for initalization of the replication manager. It will
        /// initialize listeners (UDP for broadcast from other replication managers, TCP for
        /// replication manager lisening and sending to other backups). It will also look for primary.
        /// </summary>
        /// <param name="associatedServer">The associated server that initialized this replication manager.</param>
        public ReplicationManager(ServerProgram associatedServer)
        {
            // Initialize properties
            this.serversAddresses = new List<IPAddress>();

            // assoicate the server with this replication manager
            thisServer = associatedServer;

            // Run listening to other replication managers in it's own thread
            Thread tcpBackupListenThread = new Thread(() =>
            {
                ListenToReplicationManagers();
            });

            // Start thread lisening for other backup replication managers TCP messages
            tcpBackupListenThread.Start();

            // Start lisening and broadcasting for UDP channel as well.
            BroadcastForReplication replicationManagerUDP = new BroadcastForReplication(true, PORT_NUMBER_FOR_BROADCASTING_UDP, this);


        }

        /// <summary>
        /// This method is used to initialize replication depending on whether it's a server or not.
        /// </summary>
        /// <param name="isServerPrimary">A bool for whether server is primary or not.</param>
        public void InitializeReplicationManager(bool isServerPrimary, IPAddress primaryServerIP)
        {
            if (!isServerPrimary)
            {
                // Add Primary server ip address to back up that was passed from udp listener.
                serversAddresses.Add(primaryServerIP);

                // Timer for checking if primary is there
                timerForCheckingPrimaryExistence = new Timer(CheckServerExistence, null, TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS), TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS));

                // send Initial Request when backup in initalized
                foreach (string requestMessage in MESSAGES_SENT_AND_RECEIEVED_BY_A_NEW_BACKUP)
                {
                    SendToServer(requestMessage);
                }
            }
            else
            {
                // Add this server ip address to list of ips
                serversAddresses.Add(thisServer.IPAddr);

                // start listening on this primary server to other backups
                thisServer.StartListen();
            }
        }

        /// <summary>
        /// This method starts the timer that checks all backup servers.
        /// </summary>
        internal void StartTimerPrimaryCheckingBackups()
        {
            timerForCheckingReplicasExistence = new Timer(CheckBackupExistence, null, TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS), TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS));
        }

        /// <summary>
        /// This method is responsible for listening to clients(other replication managers)
        /// for their requests.
        /// </summary>
        /// <param name="backupClient">Client sending messages to this replication manager</param>
        public void EstablishConnection(TcpClient backupClient)
        {
            // Message received while listening
            string requestMessage = string.Empty;

            try
            {
                // Receive message from the client
                TCPMessageHandler tcpClientMessageHandler = new TCPMessageHandler();
                requestMessage = tcpClientMessageHandler.RecieveMessage(backupClient);
                Console.WriteLine(requestMessage);
            }
            catch(Exception e)
            {
                Console.WriteLine("Establish Connection exception in receive message");
            }

            // Depending on whether server is primary or backup parse messages accrodingly
            if (thisServer.isPrimaryServer && 
                (requestMessage.StartsWith(REQ_BACKUP) ||
                requestMessage.StartsWith(REQ_NAMES) ||
                requestMessage.StartsWith(REQ_GAMESESSIONS) ||
                requestMessage.StartsWith(REQ_MATCH) ||
                requestMessage.StartsWith(REQ_CHECK)))
            {
                // parse request message and get a response.
                string responseMessage = parseRequestMessageForPrimary(requestMessage);

                // In case backup is checking primary existence then 
                if (requestMessage.StartsWith(REQ_CHECK))
                {
                    try
                    {
                        // Send empty response back
                        TCPMessageHandler tcpClientMessageHandler = new TCPMessageHandler();
                        tcpClientMessageHandler.SendResponse("", backupClient);
                    }
                    catch(Exception e)
                    {
                        // Primary received backup
                        // Primary received name
                        // Primary received session
                        // Primary received match
                        // backup received addresses
                        // backup received update-backup
                        // backup received session
                        // backup received match
                        // backup received names
                        Console.WriteLine("check message coming to primary failed");
                        Console.WriteLine("Failed");
                    }

                    backupClient.Close();
                }
                else
                {
                    try
                    {
                        backupClient.GetStream().Write(new byte[1], 0, 0);
                    }
                    catch
                    {
                        // Primary received backup
                        // Primary received name
                        // Primary received session
                        // Primary received match
                        // backup received addresses
                        // backup received update-backup
                        // backup received session
                        // backup received match
                        // backup received names
                        Console.WriteLine("Primary is receiving message other than check and is catching an exception");
                        Console.WriteLine("Failed");
                    }
                    backupClient.Close();

                    Console.WriteLine("Sending to all backups {0}", responseMessage);

                    // Send to all backups
                    SendToBackupsAndCheckDeadOnes(responseMessage);
                }
                    
            }
            // Server is not primary
            else if (!thisServer.isPrimaryServer &&
                (requestMessage.StartsWith(RES_ADDRESSES) ||
                requestMessage.StartsWith(RES_NAMES) ||
                requestMessage.StartsWith(RES_GAMESESSIONS) ||
                requestMessage.StartsWith(RES_MATCH) ||
                requestMessage.StartsWith(REQ_UPDATE_BACKUP) ||
                requestMessage.StartsWith(REQ_CHECK)))
            {

                // in case you received an update for for backups then set flag
                // that checks if you are second or not in the list when primary dies
                if (requestMessage.StartsWith(REQ_UPDATE_BACKUP))
                {
                    backupWasUpdated = true;
                }
                parseResponseMessageForBackup(requestMessage);

                try
                {
                    // Send garbage response back 
                    backupClient.GetStream().Write(new byte[1], 0, 0);
                }
                catch
                {
                    // Primary received backup
                    // Primary received name
                    // Primary received session
                    // Primary received match
                    // backup received addresses
                    // backup received update-backup
                    // backup received session
                    // backup received match
                    // backup received names
                    Console.WriteLine("Catching exception in receving a message for non primary");
                    Console.WriteLine("Failed");
                }
                backupClient.Close();
            }
        }
            
           
        /// <summary>
        /// This method takes a request that was sent through the network from backup to primary and parses it 
        /// and return a response to it.
        /// </summary>
        /// <param name="requestMessage">This parameter contains what was sent through the network.</param>
        /// <returns>response message</returns>
        private string parseRequestMessageForPrimary(string requestMessage)
        {
            string requestType;
            string messageParam;
            string responseMessage = string.Empty;

            // Get request type
            MessageParser.ParseNext(requestMessage, out requestType, out messageParam);

            // Incoming message "backup" from one of the backup servers to primary server.
            if (requestType == REQ_BACKUP)
            {
                // get IP Address of the backup server from message parameters
                string ipAddressString = messageParam;

                // Convert IP address from string to IPAddress
                IPAddress ipAddr;
                if (!IPAddress.TryParse(ipAddressString, out ipAddr))
                {
                    // Console.WriteLine("ERROR");
                }
                else
                {
                    // Add backup ip address to primary server list
                    serversAddresses.Add(ipAddr);

                    // Create a response back to the replicationManager of the backup server
                    // add required information to be sent back
                    responseMessage = RES_ADDRESSES + SEPERATOR_BETWEEN_WORDS;

                    // Send backup servers ip addresses starting from first backup server exculding primary server
                    responseMessage += MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), SEPERATOR_BETWEEN_PLAYERS);
                }
            }
            else if (requestType.Equals(REQ_NAMES))
            {
                // Parse player names
                responseMessage += RES_NAMES + SEPERATOR_BETWEEN_WORDS;
                responseMessage += MessageConstructor.ConstructMessageToSend(thisServer.GetPlayerNames().ToList(), SEPERATOR_BETWEEN_PLAYERS);
            }
            else if (requestType.Equals(REQ_GAMESESSIONS))
            {
                // Parse game session
                responseMessage += RES_GAMESESSIONS + SEPERATOR_BETWEEN_WORDS;
                responseMessage += MessageConstructor.ConstructMessageToSend(thisServer.GetGameSession()
                    .Select(session => session.ID + SEPERATOR_BETWEEN_WORDS + session.Players
                        .Select(player => player.ToMessageForGameSession())
                        .Aggregate(new StringBuilder(), (sb, s) =>
                        {
                            if (sb.Length > 0)
                                sb.Append(SEPERATOR_BETWEEN_PLAYERS);
                            sb.Append(s);
                            return sb;
                        })).ToList(), SEPERATOR_BETWEEN_SECTIONS_OF_MESSAGES);
            }
            else if (requestType.Equals(REQ_MATCH))
            {
                // Parse match request 
                responseMessage += RES_MATCH + SEPERATOR_BETWEEN_WORDS;
                responseMessage += MessageConstructor.ConstructMessageToSend(thisServer.GetClientWaitingForGame()
                    .Select((clientWaitingForGame, gameRequestForThisClient) => clientWaitingForGame.Count > 0 ? gameRequestForThisClient + SEPERATOR_BETWEEN_WORDS + clientWaitingForGame
                        .Select(player => player.ToMessageForGameQueue())
                        .Aggregate(new StringBuilder(), (sb, s) =>
                        {
                            if (sb.Length > 0)
                                sb.Append(SEPERATOR_BETWEEN_WORDS);
                            sb.Append(s);
                            return sb;
                        }) : "").ToList(), SEPERATOR_BETWEEN_SECTIONS_OF_MESSAGES);
            }
            else if (requestType.Equals(REQ_UPDATE_BACKUP))
            {
                // Parse update backup
                responseMessage += REQ_UPDATE_BACKUP + SEPERATOR_BETWEEN_WORDS;
                responseMessage += MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), SEPERATOR_BETWEEN_PLAYERS);
            }

            return responseMessage;
        }

        /// <summary>
        /// This method is used to parse reponse messages that are sent to Backups 
        /// </summary>
        /// <param name="reposnseMessage">Response message</param>
        private void parseResponseMessageForBackup(string reposnseMessage)
        {
            string responseType = string.Empty;
            string messageParam = string.Empty;

            // Get requestType
            MessageParser.ParseNext(reposnseMessage, out responseType, out messageParam);

            if (responseType.Equals(RES_ADDRESSES) || responseType.Equals(REQ_UPDATE_BACKUP))
            {
                // get IP Addresses of all the other servers 
                string[] arrayOfIPAddresses = messageParam.Split(',');
                List<IPAddress> allReplicaAddrTemp = new List<IPAddress>();

                // Convert IP address from string to IPAddress
                foreach (string tempIP in arrayOfIPAddresses)
                {
                    IPAddress ipAddr;
                    if (!IPAddress.TryParse(tempIP, out ipAddr))
                    {
                        // ERROR
                        // Cosole.wr
                    }
                    else
                    {
                        allReplicaAddrTemp.Add(ipAddr);
                    }
                }

                // Overwrite list of all ip servers
                serversAddresses = allReplicaAddrTemp;

            }
            else if (responseType == RES_NAMES)
            {
                ParseServerResponsePlayerNames(messageParam);

                // Print them out
                foreach (string player in thisServer.GetPlayerNames())
                {
                    Console.WriteLine("Backup received this player {0}", player);
                }
                
            }
            else if (responseType == RES_GAMESESSIONS)
            {
                ParseServerResponseGameSession(messageParam);
            }
            else if(responseType == RES_MATCH)
            {
                ParseServerResponseGameMatches(messageParam);
               
            }
        }

        /// <summary>
        /// This method is parsing player names coming from primary 
        /// </summary>
        /// <param name="message"></param>
        public void ParseServerResponsePlayerNames(string message)
        {
            // get player names
            string[] arrayOfPlayerNames = message.Split(',');

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

        /// <summary>
        /// This method is responsible of parsing messages for game session coming through the channel.
        /// </summary>
        /// <param name="message">Message to be parsed</param>
        public void ParseServerResponseGameSession(string message)
        {
            // split games sessions
            string[] arrayOfSessions = message.Split(new string[] { SEPERATOR_BETWEEN_SECTIONS_OF_MESSAGES, "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Temp variable for game session
            List<GameSession> tempGameSession = new List<GameSession>();

            foreach (string tempSession in arrayOfSessions)
            {
                // Split each game session by comma serperator
                string[] arrayOfSpecificSession = tempSession.Split(new string[] { SEPERATOR_BETWEEN_PLAYERS }, StringSplitOptions.RemoveEmptyEntries);
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
                    string[] arrayOfGameSessionAndPlayerSpecificInfo = arrayOfSpecificSession[gameSessionAndPlayerInfoIndex].Split(new string[] { SEPERATOR_BETWEEN_WORDS }, StringSplitOptions.RemoveEmptyEntries); ;

                    ClientInfo player;
                    if (extraIndexForGameID == 1)
                    {
                        gameID = arrayOfGameSessionAndPlayerSpecificInfo[0];
                        gameSession = new GameSession(int.Parse(gameID));
                    }

                    // Initialize player using information parsed from message
                    player = new ClientInfo(IPAddress.Parse(arrayOfGameSessionAndPlayerSpecificInfo[0 + extraIndexForGameID]),
                        int.Parse(arrayOfGameSessionAndPlayerSpecificInfo[1 + extraIndexForGameID]),
                        arrayOfGameSessionAndPlayerSpecificInfo[2 + extraIndexForGameID],
                        int.Parse(arrayOfGameSessionAndPlayerSpecificInfo[3 + extraIndexForGameID]));

                    // After extracting gameID, index goes back to zero.
                    extraIndexForGameID = 0;

                    // add to players in this game session
                    players.Add(player);
                }

                // After extracting gameID, index goes back to zero.
                extraIndexForGameID = 1;

                // Add to the gamesession
                gameSession.SetPlayers = players;

                // Add game session to this server game sessions list
                tempGameSession.Add(gameSession);
            }

            // Add to game session of server
            thisServer.SetGameSession(tempGameSession.ToArray());
        }

        /// <summary>
        /// This method parses game queue and update this server queue.
        /// </summary>
        /// <param name="message">Message that will be parsed.</param>
        public void ParseServerResponseGameMatches(string message)
        {
            // Split game queue by delimiter comma
            string[] arrayOfGameQueues = message.Split(new string[] { SEPERATOR_BETWEEN_SECTIONS_OF_MESSAGES, "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Queue that will be replaced with the old one after parsing
            ObservableCollection<ConcurrentQueue<ClientInfo>> tempQueues = new ObservableCollection<ConcurrentQueue<ClientInfo>>();

            // this will hold the game capacity that was requested by clients.
            int gameCapacity = -1;

            // Constants used in this funciton only
            const int PLAYER_INFO_COUNT = 5;
            const string FALSE = "0";

            // Use an integer to differ between string with gameID and without
            // First info will contain a game ID
            int extraIndexForGameCapacity = 1;

            foreach (string gameQueue in arrayOfGameQueues)
            {
                // Split by seperator
                string[] arrayOfGameQueue = gameQueue.Split(new string[] { SEPERATOR_BETWEEN_WORDS }, StringSplitOptions.RemoveEmptyEntries);

                // Check if this is the first occurence 
                if (extraIndexForGameCapacity == 1)
                {
                    // get game capacity
                    gameCapacity = int.Parse(arrayOfGameQueue[0]);

                    // Add queue depedning of game capacity
                    for (int i = tempQueues.Count; i <= gameCapacity; i++)
                    {
                        tempQueues.Add(new ConcurrentQueue<ClientInfo>());
                    }
                }

                // after extracting game ID, get player info
                for (int i = 1; i < arrayOfGameQueue.Count(); i += PLAYER_INFO_COUNT)
                {
                    ClientInfo player;
                    // For every five enteries get the relevent info
                    player = new ClientInfo(IPAddress.Parse(arrayOfGameQueue[i]),
                        int.Parse(arrayOfGameQueue[i + 1]),
                        arrayOfGameQueue[i + 2],
                        int.Parse(arrayOfGameQueue[i + 3]),
                        arrayOfGameQueue[i + 4].Equals(FALSE) ? false : true);

                    // add player to the queue of this backup
                    tempQueues[gameCapacity].Enqueue(player);
                }

                // After extracting gameID, index goes back to zero.
                extraIndexForGameCapacity = 1;
            }

            // Overwrite game queue with the one received.
            thisServer.SetClientsWaitingForGame(tempQueues);
        }

        

        /// <summary>
        /// This method send to server a message.
        /// </summary>
        /// <param name="tempMsg">what replicas are trying to send as clients</param>
        public void SendToServer(string messageToSend)
        {
            try
            {
                string messageToBeSent = string.Empty;

                // Construct message. all messages sent to server are requests with one key word
                // backup is the only that needs to inlcude backup address.
                if (messageToSend.Equals(REQ_BACKUP))
                {
                    messageToBeSent = MessageConstructor.ConstructMessageToSend(new List<string>() { messageToSend, thisServer.IPAddr.ToString() });
                }
                else
                {
                    messageToBeSent = MessageConstructor.ConstructMessageToSend(new List<string>() { messageToSend });
                }
                TCPMessageHandler tcpMessageHandler = new TCPMessageHandler();
                string responseMessage = tcpMessageHandler.SendMessage(serversAddresses[0], PORT_NUMBER_FOR_LISTENING_AND_SENDING, messageToBeSent);
                // Prepare another response to backups
                parseResponseMessageForBackup(responseMessage);
            }
            catch (Exception e)
            {
                if (messageToSend.Equals(REQ_CHECK))
                {
                    throw e;
                }
                //Console.WriteLine("sending from replica to primary for replica, name, and session is not working {0}", e.Message);
            }
        }

        /// <summary>
        /// This method get triggered for check and update backup
        /// </summary>
        /// <param name="tempMsg"></param>
        public void SendToBackUPs(string updateType)
        {
            // Construct a message to be sent based on type of update
            string messageUpdate = string.Empty;

            // In case of check then message won't have anything while others will have list of ip addresses
            if (updateType != REQ_CHECK)
            {
                messageUpdate = updateType + SEPERATOR_BETWEEN_WORDS + MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), SEPERATOR_BETWEEN_PLAYERS);
            }
            else
            {
                messageUpdate = REQ_CHECK + REQUESTS_AND_RESPONSES_SUFFIX;
            }
            // Send to all backups
            SendToBackupsAndCheckDeadOnes(messageUpdate);
        }

        /// <summary>
        /// This method gets triggered whenever a change in the game state or the list of backup servers happen
        /// </summary>
        /// <param name="tempMsg"></param>
        public void SendToBackUPsGameState(string updateType)
        {
            // Construct a message to be sent based on type of update
            string messageUpdate = string.Empty;
            messageUpdate = parseRequestMessageForPrimary(updateType);

            // Send to all backups 
            SendToBackupsAndCheckDeadOnes(messageUpdate);

        }

        /// <summary>
        /// This method will call send to replicas message to send a message and it will check if
        /// there are dead servers and update accrodingly
        /// </summary>
        /// <param name="message"></param>
        private void SendToBackupsAndCheckDeadOnes(string message)
        {
            List<IPAddress> backupsIPs = serversAddresses.Where((backup, indexOfBackup) => indexOfBackup != 0 && !backup.Equals(thisServer.IPAddr)).ToList();
            if (SendToReplicationManagers(backupsIPs, message))
            {
                // Send to everybody the new state if something changed.
                // Construct a message to be sent based on type of update
                string updateAddresses = REQ_UPDATE_BACKUP + SEPERATOR_BETWEEN_WORDS + MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), SEPERATOR_BETWEEN_PLAYERS);

                // Update backups with new server ip list
                SendToReplicationManagers(serversAddresses, updateAddresses);
            }
        }

        /// <summary>
        /// This method sends to all replication managers of servers. 
        /// </summary>
        /// <param name="replicationManagersAddresses">Replication managers to send to</param>
        /// <param name="message">Message to be sent.</param>
        /// <returns>Returns boolean indicating whether an update to all backups servers should happen.</returns>
        private bool SendToReplicationManagers(List<IPAddress> replicationManagersAddresses, string message)
        {
            // flag to indicate if we should update replication managers in case there are dead ones
            bool updateReplicationManagers = false;


            // Send message to every replication manager
            Parallel.ForEach(replicationManagersAddresses, (backupIP) =>
            {
            // Send times in case there are some of them who are dead
            int counterForSendingTimes = 0;
                while (true)
                {
                    try
                    {
                        TCPMessageHandler tcpMessageHandler = new TCPMessageHandler();
                        string responseOfBackUpToServerResponseStr = tcpMessageHandler.SendMessage(backupIP, PORT_NUMBER_FOR_LISTENING_AND_SENDING, message);
                        break;
                    }
                    catch (Exception ex)
                    {
                    // In the case there is an exception that is a socket or Input output 
                    // then client is dead and deal with that accordingly.
                    if (ex is SocketException || ex is IOException)
                        {
                        // increment times of failing
                        counterForSendingTimes++;

                        // Remove dead backups
                        if (counterForSendingTimes == SEND_TO_DEAD_BACKUPS_FOR)
                            {
                                serversAddresses.Remove(backupIP);
                                updateReplicationManagers = true;
                                break;
                            }
                        }
                        else
                        {
                        //Console.WriteLine("another exception");
                        throw ex;
                        }
                    }
                }
            });
            
            return updateReplicationManagers;
            
        }

        /// <summary>
        /// This will be used to listen on incoming requests from replication managers of servers.
        /// </summary>
        public void ListenToReplicationManagers()
        {
            // Initialize a new tcp Listener
            TcpListener rmListener = new TcpListener(thisServer.IPAddr, PORT_NUMBER_FOR_LISTENING_AND_SENDING) ;
            rmListener.Start();

            // Listen to incoming requests.
            while (true)
            {
                //Console.WriteLine("Listening");
                // Receive a client 
                TcpClient backupClient = rmListener.AcceptTcpClient();
                
                // receive messages and parse them in a new Thread.
                new Thread(() =>
                {
                    EstablishConnection(backupClient);
                }).Start();
                
            }
        }

        /// <summary>
        /// This method is responsible for making a backup server a primary.
        /// </summary>
        public void MakeThisServerPrimary()
        {
            // Update addresses by removing primary.
            serversAddresses.RemoveAt(0);

            // Set flag for checking if this server is primary or not.
            thisServer.isPrimaryServer = true;
            
            // Start timer that checks primary existence
            timerForCheckingPrimaryExistence.Change(Timeout.Infinite, Timeout.Infinite);

            // Update backup servers with the newly elected primary
            SendToBackUPs(REQ_UPDATE_BACKUP);

            // Initialize timer for primary to check backup servers existence
            timerForCheckingReplicasExistence = new Timer(CheckBackupExistence, null, TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS), TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS));

            // Server start listening 
            thisServer.StartListen();
        }

        /// <summary>
        /// This method checks that the primary server exists at all time.
        /// </summary>
        /// <param name="state">This parameter has to be passed even though we don't need it here.</param>
        private void CheckServerExistence(object state)
        {
            // Add a lock for timer callback queueing issues with threads.
            lock (checkPrimaryCallbackLock)
            {
                try
                {
                    // Check primary server existence
                    SendToServer(REQ_CHECK);

                    // Flag for when backup have been updated with the new addresses so that it won't 
                    // run into race conditions when checking its position.
                    backupWasUpdated = false;
                }
                catch (Exception ex)
                {
                    // Check if exception is of certain types
                    if (ex is SocketException || ex is IOException)
                    { 
                        // in the case that this backup is the second to take over
                        // It will be the new one
                        if (!backupWasUpdated && serversAddresses[NEXT_BACKUP_INDEX].Equals(thisServer.IPAddr))
                        {
                            // Console.WriteLine("This server is becoming a primary");
                            // Make this backup the new primary
                            MakeThisServerPrimary();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method is the callback function for the timer that checks all backup servers.
        /// </summary>
        /// <param name="state"></param>
        private void CheckBackupExistence(object state)
        {
            // lock sending for timer not to queue multiple threads in timer
            lock(checkBackupCallbackLock)
            {
                // Send Check from primary to all connected servers
                SendToBackUPs(REQ_CHECK);
            }
        }

    }
}
