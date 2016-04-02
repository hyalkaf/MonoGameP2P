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
using SharedFunctionailty;

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
        public static List<IPAddress> serversAddresses = new List<IPAddress>();

        // This timer will be running every 5 seconds to check the primary server's existence
        // This will be used when primary server is died or out of connection.
        //private static readonly int CHECK_MESSAGE_INTERVAL = 5000;
        private Timer timerForCheckingPrimaryExistence;

        //// This timer is used for timing out broadcast messages for finding primary server
        //// in the local network. Once time has passed after broadcasting without receiving
        //// responses then it will run its callback making the server primary. 
        //private static readonly int FINDING_PRIMARY_COUNTDOWN = 2000;
        //private Timer timerForFindingPrimary;

        // lock object for callback method that checks primary existence.
        // this will prevent multiple threads from queueing the callback.
        private Object checkPrimaryCallbackLock = new Object();

        //// TODO: Write a method to calculate IP address of broadcast so you don't
        //// have to do it every time.
        //// IP Endpoints that will be used to send broadcast messages and another one for receiving.
        //IPEndPoint sendingIP = new IPEndPoint(IPAddress.Parse("10.1.15.255"), 15000);
        //IPEndPoint receivingIP = new IPEndPoint(IPAddress.Any, 0);

        // GLOBAL variables for states of the program
        //// primaryFound will be used to not run the same code multiple times in case 
        //// Primary was detected. TODO: There is a better way for doing this.
        //bool primaryFound = false;
        //// isUdpResponseReceived will be used to figure out whether broadcase messages 
        //// where received by others from their responses.
        //bool isUdpResponseReceived = false;
        // backupWasUpdated is needed to prevent the case where a backup replication manager
        // receives an update for a new elected primary while it still has queued callbacks for 
        // check messages making it become the primary in that case. this will happend when replication
        // managers have almost the exact starting time checking primary existence timer.
        bool backupWasUpdated = false;

        // Request and response messsages between backup servers and primary server
        // These are mentioned in our design document
        // REQ_BACKUP is a request message that will be sent whenever a new backup is initialized
        // It will be sending this request message with its own IP address to the primary.
        private static readonly string REQ_BACKUP = "backup";
        // RES_ADDRESSES is a response message that will be sent on the request of a backup from
        // The backup server. it will contain all ip addresses of all backups including primary server.
        private static readonly string RES_ADDRESSES = "address";
        // REQ_NAMES is a request and response message that will be sent from backup to primary
        // as well as from primary to backup. As a request from backup to server it will not 
        // contain any information. As a response, it will contain the names of all players currently
        // in the system that are waiting for a game.
        private static readonly string REQ_NAMES = "name";
        // REQ_GAMESESSIONS is a request and response message. As a request from backup to server it will not 
        // contain any information. As a response, it will the information stored in the game session
        // like sessionID, and players (player name, ID, Port and IP) that are currently
        // playing a game with that unique sessionID.
        private static readonly string REQ_GAMESESSIONS = "session";
        // REQ_CHECK is a request message from backup replication manager to the primary server 
        // checking if it still exists and it can't receive and respond to messages.
        private static readonly string REQ_CHECK = "check";
        // REQ_MATCH is a request and a response message. As a request from backup to server it will not 
        // contain any information. As a response, it will contain information about the queued players
        // waiting to be assigned and matched to other players. This information is game capacity or number
        // of matched clients, and players info in that specific game capacity which is player name, player ID
        // port and IP address.
        public static readonly string REQ_MATCH = "match";
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

        // Server program assoicated with this replication manager
        public ServerProgram thisServer;

        /// <summary>
        /// Main constructor for initalization of the replication manager. It will
        /// initialize listeners (UDP for broadcast from other replication managers, TCP for
        /// primary sending messages). It will also look for primary.
        /// </summary>
        /// <param name="associatedServer">The associated server that initialized the this replication manager.</param>
        public ReplicationManager(ServerProgram associatedServer)
        {
            // assoicate the server with this replication manager
            thisServer = associatedServer;

            // Run listening on its own thread
            new Thread(() =>
            {
                ListenReplica();
            }).Start();

            // Broadcast to local network trying to find if a primary exists or not.
            // Start Listening for udp broadcast messages after enabling broadcast
            // receiveBroadcastUDPClient.EnableBroadcast = true;
            /*new Thread(() =>
            {
                while(true)
                {
                    StartListeningUdp();
                }
            }).Start();*/
            UDPWrapper replicationManagerUDP = new UDPWrapper(true, 15000, this);



            // TODO: Send multiple times for udp
            /*timerForFindingPrimary = new Timer(timerCallBackForFindingPrimary, "isPrimary", FINDING_PRIMARY_COUNTDOWN, Timeout.Infinite);
            for (int i = 0; i < 3; i++)
            {
                if (!isUdpResponseReceived)
                {
                    Broadcast("isPrimary");
                }
                Thread.Sleep(500);
            }*/

            
  
        }

        /// <summary>
        /// This method is used to initialize replication depedning on whether it's a server.
        /// </summary>
        /// <param name="isServerPrimary">A bool for whether server is primary or not.</param>
        public void InitializeReplicationManager(bool isServerPrimary, IPAddress primaryServerIP)
        {
            if (!isServerPrimary)
            {
                if (!serversAddresses.Exists(e => e.Equals(primaryServerIP)))
                { 
                    // Add Primary server ip address to replica
                    //TODO dont need this, get list update from primary
                    serversAddresses.Add(primaryServerIP);

                    // Timer for checking if primary is there
                    timerForCheckingPrimaryExistence = new Timer(CheckServerExistence, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

                    // secondary replica sends a replica request
                    DecideOnMessagesSendFromBackUpToServer(true);
                }
                
            }
            else
            {
                addReplica(thisServer);

                // Make this server start listening
                thisServer.StartListen();
            }
        }

        /// <summary>
        /// This method is responsible for taking a socket connection and receiving incoming message parse it and then send a response.
        /// </summary>
        /// <param name="sock">Socket that was listened on</param>
        public void EstablishConnection(TcpClient replicaClient)
        {
            Console.WriteLine("Establishing Connection with {0} {1}", (replicaClient.Client.LocalEndPoint as IPEndPoint).Address, (replicaClient.Client.RemoteEndPoint as IPEndPoint).Address);

            NetworkStream netStream = replicaClient.GetStream();

            replicaClient.ReceiveBufferSize = 4096;
            byte[] bytes = new byte[replicaClient.ReceiveBufferSize];

            netStream.Read(bytes, 0, (int)replicaClient.ReceiveBufferSize);

            string requestMessage = Encoding.ASCII.GetString(bytes).Trim();
            requestMessage = requestMessage.Substring(0, requestMessage.IndexOf("\0")).Trim();


            //StringBuilder sb = new StringBuilder();

            //byte[] buffer = new byte[SIZE_OF_BUFFER];
            //int bytesRead = sock.Receive(buffer);

            //sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            //Console.WriteLine("Message that was listened to {0}", sb.ToString());

            //string requestMessage = sb.ToString().Trim().ToLower();

            byte[] responseMessageForBackupOrCheck = new byte[SIZE_OF_BUFFER];

            if (requestMessage.StartsWith(REQ_CHECK))
            {
                responseMessageForBackupOrCheck = parseRequestMessageForPrimary(requestMessage);
            }
            // Here we want to send back to all backups
            if ((requestMessage.StartsWith(REQ_NAMES)
                || requestMessage.StartsWith(REQ_GAMESESSIONS)
                || requestMessage.StartsWith(REQ_MATCH)
                || requestMessage.StartsWith(REQ_BACKUP))
                && thisServer.isPrimaryServer)
            {
                // add success message and respond back to the server.
                replicaClient.GetStream().Write(new byte[1],0,0);

                replicaClient.Close();

                // Get appeopraite response
                byte[] responseMessage = parseRequestMessageForPrimary(requestMessage);

                // Accumlate backup indexes from the list of backup ips in case they are died
                List<int> indexOfDeadBackupServers = new List<int>();

                // Send all backups updated info
                for (int j = 1; j < serversAddresses.Count; j++) 
                {
                    // 
                    IPAddress backupIP = serversAddresses[j];

                    

                    try
                    {
                        TcpClient primaryClientToBackup = new TcpClient();
                        primaryClientToBackup.Connect(backupIP, 8000);

                        //Console.WriteLine("Sending to every backup this {0}", responseMessage);

                        Stream stm = primaryClientToBackup.GetStream();

                        ASCIIEncoding asen = new ASCIIEncoding();

                        stm.Write(responseMessage, 0, responseMessage.Length);
                        byte[] responseOfBackUpToServerResponse = new byte[SIZE_OF_BUFFER];

                        // Receive response from primary
                        int k = stm.Read(responseOfBackUpToServerResponse, 0, SIZE_OF_BUFFER);

                        string responseOfBackUpToServerResponseStr = "";
                        char c = ' ';
                        for (int i = 0; i < k; i++)
                        {
                            c = Convert.ToChar(responseOfBackUpToServerResponse[i]);
                            responseOfBackUpToServerResponseStr += c;
                        }

                        primaryClientToBackup.Close();
                        // TODO: Test response again.
                    }
                    catch (SocketException)
                    {
                        // Remove dead backups
                        indexOfDeadBackupServers.Add(j);
                    }

                    bool areDead = false;
                    // Remove all dead backups if there is any
                    foreach (int deadBackupind in indexOfDeadBackupServers)
                    {
                        // Ping each backup again 
                        Ping pingBackups = new Ping();
                        PingReply reply = pingBackups.Send(serversAddresses[deadBackupind]);
                        if (!reply.Status.Equals(IPStatus.Success))
                        {
                            serversAddresses.RemoveAt(deadBackupind);
                            areDead = true;
                        }

                    }

                    // Send messages again
                    // Send all backups updated info
                    // TODO: Refactor this code.
                    if (areDead)
                    {
                        for (int z = 1; z < serversAddresses.Count; z++)
                        {
                            // Catch exceptions when backup is not there
                            // 
                            try
                            {
                                sendMessage(serversAddresses[j], Encoding.ASCII.GetString(responseMessage));
                            }
                            catch (SocketException)
                            {
                                // Remove the backup server that is not responding to messages
                                indexOfDeadBackupServers.Add(j);
                            }
                        }
                    }

                }

                foreach(int deadBackupInd in indexOfDeadBackupServers)
                {
                    // Ping each backup again 
                    Ping pingBackups = new Ping();
                    PingReply reply = pingBackups.Send(serversAddresses[deadBackupInd]);
                    if (!reply.Status.Equals(IPStatus.Success))
                    {
                        serversAddresses.RemoveAt(deadBackupInd);
                    }
                }
            }
            // Messages receivied from primary by backup after listening
            else if ((requestMessage.StartsWith(REQ_NAMES)
                    || requestMessage.StartsWith(REQ_GAMESESSIONS)
                    || requestMessage.StartsWith(REQ_MATCH)
                    || requestMessage.StartsWith(REQ_UPDATE_BACKUP)
                    || requestMessage.StartsWith(RES_ADDRESSES))
                    && !thisServer.isPrimaryServer)
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

                replicaClient.Close();

            }
            else
            {
                // ????
                replicaClient.Client.Send(responseMessageForBackupOrCheck);

                replicaClient.Close();
            }
           


        }

        /// <summary>
        /// This method takes a string that was sent through the network and parses it and return a respons to it.
        /// </summary>
        /// <param name="requestMessage">This parameter contains what was sent through the network.</param>
        /// <returns>response message in bytes array.</returns>
        private byte[] parseRequestMessageForBackup(string requestMessage)
        {
            string requestType;
            string messageParam;
            string responseMessage = string.Empty;
            byte[] b = new byte[SIZE_OF_BUFFER];

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

            

            return b;
        }

        /// <summary>
        /// This method takes a request that was sent through the network from backup to primary and parses it 
        /// and return a response to it.
        /// </summary>
        /// <param name="requestMessage">This parameter contains what was sent through the network.</param>
        /// <returns>response message in bytes array.</returns>
        private byte[] parseRequestMessageForPrimary(string requestMessage)
        {
            string requestType;
            string messageParam;
            string responseMessage = string.Empty;
            byte[] b = new byte[SIZE_OF_BUFFER];

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

                    ASCIIEncoding asen = new ASCIIEncoding();

                    b = asen.GetBytes(responseMessage + "\n\n");
                }
            }
            else if (requestType == REQ_NAMES || requestType == REQ_GAMESESSIONS || requestType == REQ_MATCH)
            {
                responseMessage = ConstructPrimaryMessageToBackupBasedOnRequestType(requestType);

                ASCIIEncoding asen = new ASCIIEncoding();

                b = asen.GetBytes(responseMessage + "\n\n");
            }

            return b;
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

                //if (responseType.Equals(REQ_UPDATE_BACKUP))
                //{
                //    primaryServerIp = serversAddresses[0];
                //}

                /*foreach (IPAddress ip in serversAddresses)
                {
                    Console.WriteLine("in method parseResponseMessageForBackup, server addresses are {0}", ip);
                }*/
            }
            else if (responseType == REQ_NAMES || responseType == REQ_GAMESESSIONS || responseType == REQ_MATCH)
            {
                if (!string.IsNullOrEmpty(messageParam) || responseType.Equals(REQ_MATCH))
                {
                    ParseServerResponseMessageToBackUpForGameInfo(responseType, messageParam);
                }
                
            }

        }


        ///// <summary>
        ///// This method is used to parse reponse messages after sending requests. 
        ///// </summary>
        ///// <param name="reposnseMessage">Response messages</param>
        //private void parseResponseMessageForPrimary(string reposnseMessage)
        //{
        //    throw new NotImplementedException();
        //}

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
        /// This method constructs broadcasting messages.
        /// </summary>
        /// <returns>A string that will be send to all existing IP Addresses in the network.</returns>
        private string ConstructBroadcastMessage()
        {
            return "isPrimary" + "\n" + "\n";
        }

        /// <summary>
        /// This method constructs a message that will be sent from primary to replica for name request.
        /// </summary>
        /// <param name="names">List of names of players to be sent from primary server to replica.</param>
        /// <returns>Message to be sent to the replica</returns>
        private string ConstructPrimaryMessageNames(ObservableCollection<string> names)
        {
            string responseMessage = "name" + " ";

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

                // Debug
                /*foreach (string ply in thisServer.GetPlayerNames())
                {
                    Console.WriteLine("In method ParseServerResponseMessageToBackUpForGameInfo, response is name received player as backup {0}", ply);
                }*/
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

                // Debug
                /*foreach (GameSession sess in thisServer.GetGameSession())
                {
                    foreach(ClientInfo cli in sess.Players)
                    {
                        Console.WriteLine("backup received session players of ID {0} and {1} {2} {3} {4}", sess.ID, cli.IPAddr, cli.ListeningPort, cli.PlayerId, cli.PlayerName);
                    }
                }*/
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
        /// 
        /// </summary>
        /// <param name="replica"></param>
        public void addReplica(ServerProgram replica)
        {
            serversAddresses.Add(replica.ipAddr);
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
                    //checkTimerCounter = 0;
                    backupWasUpdated = false;
                }
                catch (SocketException)
                {
                    // In this case: server must have crashed
                    // take over and become the primary 
                    // TODO: This won't work for multiple servers
                    if (!backupWasUpdated && serversAddresses[1].Equals(thisServer.ipAddr))
                    {
                       // if (checkTimerCounter.Equals(0))
                       // {
                        MakeThisServerPrimary();
                       // }
                    }
                    /*// TODO: in the case where you are not second to primary 
                    // Then we keep a counter for trying
                    else
                    {
                        checkTimerCounter++;
                    }*/
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
                messageToBeSent = "name" + " "; 
            }
            else if (replicaMsg.StartsWith(REQ_BACKUP))
            {
                // Message to be sent 
                messageToBeSent = "backup" + " " + thisServer.ipAddr;
            }
            else if (replicaMsg.StartsWith(REQ_CHECK))
            {
                // Message to be sent 
                messageToBeSent = "check" + " ";
            }
            else if (replicaMsg.StartsWith(REQ_GAMESESSIONS))
            {
                messageToBeSent = "session" + " ";
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

            //Console.WriteLine("in method SendFromReplicaToServerAndParseResponse, message to be sent from backup to server {0}", messageToBeSent);

            // replica TCP Client for sending requests to primary server
            // Initalize a new TcpClient
            using (TcpClient replicaClient = new TcpClient())
            {

            // will send a message to the primary server
                replicaClient.Connect(serversAddresses[0], 8000);

            Stream stm = replicaClient.GetStream();

            byte[] bytesToSend = Encoding.ASCII.GetBytes(messageToBeSent);
            stm.Write(bytesToSend, 0, bytesToSend.Length);

            replicaClient.ReceiveBufferSize = SIZE_OF_BUFFER;
            byte[] bytesRead = new byte[replicaClient.ReceiveBufferSize];

            stm.Read(bytesRead, 0, (int)replicaClient.ReceiveBufferSize);

            string responseMessage = Encoding.ASCII.GetString(bytesRead);
            responseMessage = responseMessage.Substring(0, responseMessage.IndexOf("\0")).Trim();

            //Console.WriteLine("in method SendFromReplicaToServerAndParseResponse, response message {0}", responseMessage);

            // Prepare another response to backups
            parseResponseMessageForBackup(responseMessage);


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

            // Send all backups updated info
            for (int j = 1; j < serversAddresses.Count; j++)
            {
                // Catch exceptions when backup is not there
                // 
                try
                {
                    IPAddress backupIP = serversAddresses[j];
                    TcpClient primaryClientToBackup = new TcpClient();
                    primaryClientToBackup.Connect(backupIP, 8000);

                    Console.WriteLine("Sending to every backup this {0}", messageUpdate);

                    Stream stm = primaryClientToBackup.GetStream();

                    ASCIIEncoding asen = new ASCIIEncoding();

                    byte[] messageUpdateBytes = asen.GetBytes(messageUpdate);

                    stm.Write(messageUpdateBytes, 0, messageUpdateBytes.Length);
                    byte[] responseOfBackUp = new byte[SIZE_OF_BUFFER];

                    // Receive response from primary
                    int k = stm.Read(responseOfBackUp, 0, SIZE_OF_BUFFER);

                    string responseOfBackUpToServerResponseStr = "";
                    char c = ' ';
                    for (int i = 0; i < k; i++)
                    {
                        c = Convert.ToChar(responseOfBackUp[i]);
                        responseOfBackUpToServerResponseStr += c;
                    }

                    // TODO: Check if response has success

                    primaryClientToBackup.Close();
                }
                catch (SocketException)
                {
                    // Remove the backup server that is not responding to messages
                    indexOfDeadBackupServers.Add(j);
                }
            }

            bool areDead = false;
            // Remove all dead backups if there is any
            foreach (int deadBackupind in indexOfDeadBackupServers)
            {
                // Ping each backup again 
                Ping pingBackups = new Ping();
                PingReply reply = pingBackups.Send(serversAddresses[deadBackupind]);
                if (!reply.Status.Equals(IPStatus.Success))
                {
                    serversAddresses.RemoveAt(deadBackupind);
                    areDead = true;
                }
                
            }

            // Send messages again
            // Send all backups updated info
            // TODO: Refactor this code.
            if (areDead)
            {
                for (int j = 1; j < serversAddresses.Count; j++)
                {
                    // Catch exceptions when backup is not there
                    // 
                    try
                    {
                        sendMessage(serversAddresses[j], messageUpdate);
                    }
                    catch (SocketException)
                    {
                        // Remove the backup server that is not responding to messages
                        indexOfDeadBackupServers.Add(j);
                    }
                }
            }
        }


        private void sendMessage(IPAddress ip, string message)
        {
            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(ip, 8000);

            //Console.WriteLine("Sending to every backup this {0}", messageUpdate);

            Stream stm = tcpClient.GetStream();

            ASCIIEncoding asen = new ASCIIEncoding();

            byte[] messageUpdateBytes = asen.GetBytes(message);

            stm.Write(messageUpdateBytes, 0, messageUpdateBytes.Length);
            byte[] responseOfBackUp = new byte[SIZE_OF_BUFFER];

            // Receive response from primary
            int k = stm.Read(responseOfBackUp, 0, SIZE_OF_BUFFER);

            string responseOfBackUpToServerResponseStr = "";
            char c = ' ';
            for (int i = 0; i < k; i++)
            {
                c = Convert.ToChar(responseOfBackUp[i]);
                responseOfBackUpToServerResponseStr += c;
            }

            // TODO: Check if response has success

            tcpClient.Close();
        }

        /// <summary>
        /// This will be used to listen on incoming requests from the server.
        /// </summary>
        public void ListenReplica()
        {
            TcpListener rmListener = new TcpListener(thisServer.ipAddr, 8000) ;
            rmListener.Start();
            while (true)
            {
                Console.WriteLine("Listening");
                //Socket sock = rmListener.AcceptSocket();
                TcpClient replicaClient = rmListener.AcceptTcpClient();
                new Thread(() => {
                    EstablishConnection(replicaClient);
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

        /// <summary>
        /// This method will broadcast a message to all ip addresses in the local newtork.
        /// </summary>
        /// <param name = "message" > Message to be broadcasted to all local network peers.</param>
        //private void Broadcast(string message)
        //{
        //    Initialize a new udp client
        //   IPEndPoint ipEndPoint = new IPEndPoint(thisServer.ipAddr, 15000);
        //    UdpClient client = new UdpClient(ipEndPoint);
        //    client.EnableBroadcast = true;

        //    Send a request message asking if primary exists.
        //    byte[] bytes = Encoding.ASCII.GetBytes(message);

        //    Send message
        //    client.Send(bytes, bytes.Length, sendingIP);

        //    Console.WriteLine("I sent {0}", message);

        //    Close client
        //    client.Close();
        //}

        //// <summary>
        //// this method will start listening for incoming requests to check if replica is primary or not
        //// </summary>
        //private void StartListeningUdp()
        //{
        //    //receive messages
        //    byte[] bytes = receiveBroadcastUDPClient.Receive(ref receivingIP);
        //    string message = Encoding.ASCII.GetString(bytes);
        //    Console.WriteLine("I received {0}", message);
        //    // todo: disable sending messages to yourself by default
        //    if (!receivingIP.Address.Equals(thisServer.ipAddr)) ParseBroadcastMessages(message, receivingIP);
        //}

        ///// <summary>
        ///// This method will parse incoming requests that are sent using broadcase udp.
        ///// </summary>
        ///// <param name="receivedMessage">Message to be parsed</param>
        //private void ParseBroadcastMessages(string receivedMessage, IPEndPoint ip)
        //{
        //    // Parse message received 
        //    if (receivedMessage.StartsWith("isPrimary"))
        //    {
        //        isUdpResponseReceived = true;
        //        // Check if this backup server is primary
        //        if (IsPrimary())
        //        {
        //            // Send a response back
        //            // TODO: Only send to specific ip.
        //            // Don't broadcast 
        //            Broadcast("primary");
        //            // Test: send to specific ip
        //            // Initialize a new udp client
        //            //UdpClient client = new UdpClient(AddressFamily.InterNetwork);

        //            //// Send a request message asking if primary exists.
        //            //byte[] bytes = Encoding.ASCII.GetBytes("primary");

        //            //// Send message
        //            //ip.Port = 15000;
        //            //client.Send(bytes, bytes.Length, ip);

        //            //Console.WriteLine("I sent {0}", "primary");

        //            //// Close client
        //            //client.Close();


        //        }
        //    }
        //    else if (receivedMessage.StartsWith("primary") && !primaryFound)
        //    {
        //        isUdpResponseReceived = true;
        //        primaryFound = true;

        //        // Disable timer 
        //        timerForFindingPrimary.Change(Timeout.Infinite, Timeout.Infinite);

        //        // Make this server a backup
        //        thisServer.isPrimaryServer = false;

        //        // DEBUG
        //        Console.WriteLine("Primary was found, this server is backup");

        //        // Take the ip address of 
        //        primaryServerIp = ip.Address;

        //        InitializeReplicationManager(false);
        //    }
        //}

        ///// <summary>
        ///// This method is a callback for a timer where it's being called when a server doesn't get any reply when it's initialized.
        ///// The server becomes the primary server when that happens.
        ///// </summary>
        ///// <param name="state">Passed parameter to the call back -> Object</param>
        //private void timerCallBackForFindingPrimary(object state)
        //{
        //    thisServer.isPrimaryServer = true;
        //    primaryServerIp = thisServer.ipAddr; 

        //    Console.WriteLine("I'm primary");
        //    addReplica(thisServer);

        //    thisServer.StartListen();
            
        //}

    }
}
