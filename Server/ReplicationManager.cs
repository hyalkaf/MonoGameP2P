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
        private static readonly int CHECK_MESSAGE_INTERVAL_IN_SECONDS = 5;
        private Timer timerForCheckingPrimaryExistence;

        // This timer will be running every 5 seconds to check the primary server's existence
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
        // REQ_ADDRESSES is a request message from primary servers to backup servers 
        // sending them information about addresses of all backup server currently in 
        // pool of servers. This will be sent after a new backup server have entered the pool
        // and have already sent its own ip address.
        public static readonly string RES_ADDRESSES = "address";
        // REQ_NAMES is a request message that will be sent from backup to primary.
        // It will not contain any information. 
        public static readonly string REQ_NAMES = "nameRequest";
        // RES_NAMES is a response message that will be sent from primary server to all 
        // back ups
        // RES_NAMES is a response message acknowledging whoever sent the request "name" that
        // it was received correctly.
        public static readonly string RES_NAMES = "playerNames";
        // REQ_GAMESESSIONS is a request and response message. As a request from backup to server it will not 
        // contain any information. As a response, it will the information stored in the game session
        // like sessionID, and players (player name, ID, Port and IP) that are currently
        // playing a game with that unique sessionID.
        public static readonly string REQ_GAMESESSIONS = "sessionRequest";
        public static readonly string RES_GAMESESSIONS = "gameSessions";
        // REQ_CHECK is a request message from backup replication manager to the primary server 
        // checking if it still exists and it can't receive and respond to messages.
        public static readonly string REQ_CHECK = "check";
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
        public static readonly string REQ_UPDATE_BACKUP = "update-backup";
        // TODO: Add responses
        //private static readonly string RESP_SUCCESS = "success";
        // Requests that will be sent from backup to primary server every time 
        // a new backup is initalized. 
        private static readonly string[] MESSAGES_SENT_AND_RECEIEVED_BY_A_NEW_BACKUP = { REQ_BACKUP, REQ_NAMES, REQ_GAMESESSIONS, REQ_MATCH };

        // This is the default port used for broadcasting and listening to 
        // UDP messages. Make sure that firewall is not blocking it as well as
        // Cybera can receive and listen on it.
        private static readonly int PORT_NUMBER_FOR_BROADCASTING_UDP = 15000;

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
                ListenToPrimary();
            });

            // Start thread lisening for other backup replication managers TCP messages
            tcpBackupListenThread.Start();

            // Start lisening and broadcasting for UDP channel as well.
            BroadcastForReplication replicationManagerUDP = new BroadcastForReplication(true, PORT_NUMBER_FOR_BROADCASTING_UDP, this);

            if (thisServer.isPrimaryServer)
            {
                timerForCheckingReplicasExistence = new Timer(CheckBackupExistence, null, TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS), TimeSpan.FromSeconds(CHECK_MESSAGE_INTERVAL_IN_SECONDS));
            }


        }

        /// <summary>
        /// This method is used to initialize replication depedning on whether it's a server.
        /// </summary>
        /// <param name="isServerPrimary">A bool for whether server is primary or not.</param>
        public void InitializeReplicationManager(bool isServerPrimary, IPAddress primaryServerIP)
        {
            if (!isServerPrimary)
            {
                // Add Primary server ip address to replica
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
            
            string requestMessage = string.Empty;
            try
            {
                // TODO: deal with exceptions in this
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
                requestMessage.StartsWith(REQ_UPDATE_BACKUP) ||
                requestMessage.StartsWith(REQ_CHECK)))
            {
                // Get appeopraite response
                string responseMessage = parseRequestMessageForPrimary(requestMessage);

                if (requestMessage.StartsWith(REQ_CHECK))
                {
                    try
                    {
                        TCPMessageHandler tcpClientMessageHandler = new TCPMessageHandler();
                        tcpClientMessageHandler.SendResponse("", backupClient);
                    }
                    catch(Exception e)
                    {
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
                        Console.WriteLine("Failed");
                    }
                    backupClient.Close();

                    // Send to all backups
                    IEnumerable<IPAddress> backupsIPs = serversAddresses.Where((backup, indexOfBackup) => indexOfBackup != 0 && !backup.Equals(thisServer.IPAddr));
                    if (SendToReplicationManagers(backupsIPs, responseMessage))
                    {
                        // Send to everybody the new state if something changed.
                        // Construct a message to be sent based on type of update
                        string updateAddresses = REQ_UPDATE_BACKUP + " " + MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), ",");

                        //
                        SendToReplicationManagers(serversAddresses, updateAddresses);
                    }
                }
                    
            }
            // Server is not primary
            else if (!thisServer.isPrimaryServer &&
                (requestMessage.StartsWith(RES_ADDRESSES) ||
                requestMessage.StartsWith(RES_NAMES) ||
                requestMessage.StartsWith(RES_GAMESESSIONS) ||
                requestMessage.StartsWith(RES_MATCH) ||
                requestMessage.StartsWith(REQ_CHECK)))
            {

                // Update information for backup 
                // Here we are parsing request but since method is same use same
                // TODO: Update names
                if (requestMessage.StartsWith(REQ_UPDATE_BACKUP))
                {
                    backupWasUpdated = true;
                }
                parseResponseMessageForBackup(requestMessage);

                try
                {
                    backupClient.GetStream().Write(new byte[1], 0, 0);
                }
                catch
                {
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
        /// <returns>response message in bytes array.</returns>
        private string parseRequestMessageForPrimary(string requestMessage)
        {
            string requestType;
            string messageParam;
            string responseMessage = string.Empty;

            MessageParser.ParseNext(requestMessage, out requestType, out messageParam);

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
                    responseMessage += MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), ",");
                }
            }
            else if (requestType.Equals(REQ_NAMES))
            {
                responseMessage += RES_NAMES + " ";
                responseMessage += MessageConstructor.ConstructMessageToSend(thisServer.GetPlayerNames().ToList(), ",");
            }
            else if (requestType.Equals(REQ_GAMESESSIONS))
            {
                responseMessage += RES_GAMESESSIONS + " ";
                responseMessage += MessageConstructor.ConstructMessageToSend(thisServer.GetGameSession()
                    .Select(session => session.ID + " " + session.Players
                        .Select(player => player.ToMessage())
                        .Aggregate(new StringBuilder(), (sb, s) =>
                        {
                            if (sb.Length > 0)
                                sb.Append(",");
                            sb.Append(s);
                            return sb;
                        })).ToList(), "\n");
            }
            else if (requestType.Equals(REQ_MATCH))
            {
                responseMessage += RES_MATCH + " ";
                responseMessage += MessageConstructor.ConstructMessageToSend(thisServer.GetClientWaitingForGame()
                    .Select((clientWaitingForGame, gameRequestForThisClient) => clientWaitingForGame.Count > 0 ? gameRequestForThisClient + " " + clientWaitingForGame
                        .Select(player => player.ToMessage())
                        .Aggregate(new StringBuilder(), (sb, s) =>
                        {
                            if (sb.Length > 0)
                                sb.Append(",");
                            sb.Append(s);
                            return sb;
                        }) : "").ToList(), "\n");
            }
            else if (requestType.Equals(REQ_UPDATE_BACKUP))
            {
                responseMessage += REQ_UPDATE_BACKUP + " ";
                responseMessage += MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), ",");
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

            MessageParser.ParseNext(reposnseMessage, out responseType, out messageParam);

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
            else if (responseType == RES_NAMES)
            {
                ParseServerResponsePlayerNames(messageParam);
                
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
        /// 
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
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void ParseServerResponseGameSession(string message)
        {
            // split games sessions
            string[] arrayOfSessions = message.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void ParseServerResponseGameMatches(string message)
        {
            // Split game queue by delimiter comma
            string[] arrayOfGameQueues = message.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

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
        }

        

        /// <summary>
        /// This method chooses between either sending initial messages from backup to primary or 
        /// check message for server existence.
        /// </summary>
        /// <param name="tempMsg">what replicas are trying to send as clients</param>
        public void SendToServer(string messageToSend)
        {
            try
            {
                string messageToBeSent = string.Empty;
                if (messageToSend.Equals(REQ_BACKUP))
                {
                    messageToBeSent = MessageConstructor.ConstructMessageToSend(new List<string>() { messageToSend, thisServer.IPAddr.ToString() });
                }
                else
                {
                    messageToBeSent = MessageConstructor.ConstructMessageToSend(new List<string>() { messageToSend });
                }
                TCPMessageHandler tcpMessageHandler = new TCPMessageHandler();
                string responseMessage = tcpMessageHandler.SendMessage(serversAddresses[0], 8000, messageToBeSent);
                // Prepare another response to backups
                parseResponseMessageForBackup(responseMessage);
            }
            catch (Exception e)
            {
                if (messageToSend.Equals(REQ_CHECK))
                {
                    throw e;
                }
                Console.WriteLine("sending from replica to primary for replica, name, and session is not working {0}", e.Message);
            }
        }

        /// <summary>
        /// This method get triggered whenever a change in the game state or the list of backup servers happen
        /// </summary>
        /// <param name="tempMsg"></param>
        public void SendToBackUPs(string updateType)
        {
            // Construct a message to be sent based on type of update
            string messageUpdate = string.Empty;
            if (updateType != REQ_CHECK)
            {
                messageUpdate = updateType + " " + MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), ",");
            }
            else
            {
                messageUpdate = REQ_CHECK + "\n\n";
            }
            // Send to all backups
            IEnumerable<IPAddress> backupsIPs = serversAddresses.Where((backup, indexOfBackup) => indexOfBackup != 0 && !backup.Equals(thisServer.IPAddr));
            if (SendToReplicationManagers(backupsIPs, messageUpdate))
            {
                // Send to everybody the new state if something changed.
                // Construct a message to be sent based on type of update
                string updateAddresses = REQ_UPDATE_BACKUP + " " + MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), ",");

                // update everybody
                SendToReplicationManagers(serversAddresses, updateAddresses);
            }
        }

        /// <summary>
        /// This method get triggered whenever a change in the game state or the list of backup servers happen
        /// </summary>
        /// <param name="tempMsg"></param>
        public void SendToBackUPsGameState(string updateType)
        {
            // Construct a message to be sent based on type of update
            string messageUpdate = string.Empty;
            messageUpdate = parseRequestMessageForPrimary(updateType);
            // Send to all backups
            IEnumerable<IPAddress> backupsIPs = serversAddresses.Where((backup, indexOfBackup) => indexOfBackup != 0 && !backup.Equals(thisServer.IPAddr));
            if (SendToReplicationManagers(backupsIPs, messageUpdate))
            {
                // Send to everybody the new state if something changed.
                // Construct a message to be sent based on type of update
                string updateAddresses = REQ_UPDATE_BACKUP + " " + MessageConstructor.ConstructMessageToSend(serversAddresses.Select(ip => ip.ToString()).ToList(), ",");

                // update everybody
                SendToReplicationManagers(serversAddresses, updateAddresses);
            }
        }

        private bool SendToReplicationManagers(IEnumerable<IPAddress> replicationManagersAddresses, string message)
        {
            bool updateReplicationManagers = false;

            Parallel.ForEach(replicationManagersAddresses, (backupIP) =>
            {
                int counterForSendingTimes = 0;
                while (true)
                {
                    try
                    {
                        TCPMessageHandler tcpMessageHandler = new TCPMessageHandler();
                        string responseOfBackUpToServerResponseStr = tcpMessageHandler.SendMessage(backupIP, 8000, message);
                        break;
                    }
                    catch (Exception ex) 
                    {
                        if (ex is SocketException || ex is IOException)
                        {
                            counterForSendingTimes++;

                            // Remove dead backups
                            if (counterForSendingTimes == 2)
                            {
                                serversAddresses.Remove(backupIP);
                                updateReplicationManagers = true;
                                break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("another exception");
                            throw ex;
                        }
                    }
                }
            });

            return updateReplicationManagers;
            
        }

        /// <summary>
        /// This will be used to listen on incoming requests from the server.
        /// </summary>
        public void ListenToPrimary()
        {
            TcpListener rmListener = new TcpListener(thisServer.IPAddr, 8000) ;
            rmListener.Start();
            while (true)
            {
                Console.WriteLine("Listening");
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
            SendToBackUPs(REQ_UPDATE_BACKUP);

            timerForCheckingReplicasExistence = new Timer(CheckBackupExistence, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

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
                try
                {

                    SendToServer(REQ_CHECK);
                    backupWasUpdated = false;
                }
                catch (SocketException)
                {
                    // In this case: server must have crashed
                    // take over and become the primary 
                    // TODO: This won't work for multiple servers
                    if (!backupWasUpdated && serversAddresses[1].Equals(thisServer.IPAddr))
                    {
                        Console.WriteLine("This server is becoming a primary");
                        MakeThisServerPrimary();
                    }
                }
            }
        }


        private void CheckBackupExistence(object state)
        {
            SendToBackUPs(REQ_CHECK);
        }

    }
}
