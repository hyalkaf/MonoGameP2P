using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{

    public class ReplicationManager
    {
        // Primary server ip address
        public static IPAddress primaryServerIp = IPAddress.Parse("0.0.0.0");
        // CH: This way of storing all replicas might not be viable
        public static List<ServerProgram> listReplicas = new List<ServerProgram>();
        // CH: New way of storing replicas (IPAddress, Bool: online status)
        public static List<Tuple<IPAddress, bool>> allReplicaAddr = new List<Tuple<IPAddress, bool>>();
        // replica TCP Client for sending requests to primary server
        private TcpClient replicaClient;
        // Timer for running a check agansit the primary server.
        Timer timerForCheckingPrimaryExistence;
        // Timer for 
        Timer timerForFindingPrimary;
        // lock object for check messages so it won't continue sending messages on different threads
        private Object thisLock = new Object();
        private Object udpLock = new Object();
        // Requests to be sent from replica to primary server every time a new replica is initalized.
        public static readonly string[] arrayOfReplicaMessages = { "backup", "name", "session" , "queue"};
        // Udp client listening for broadcast messages
        private readonly UdpClient udpBroadcast = new UdpClient(15000);
        // IP Address for broadcasting
        IPEndPoint sendingIP = new IPEndPoint(IPAddress.Broadcast, 15000);
        IPEndPoint receivingIP = new IPEndPoint(IPAddress.Any, 0);

        // Request messsages between replicas and server
        const string REQ_BACKUP = "backup";
        const string RES_ADDRESSES = "address";
        const string REQ_NAMES = "name";
        const string REQ_GAMESESSIONS = "session";
        const string REQ_CHECK = "check";
        const string REQ_QUEUE = "queue";
        const string RESP_SUCCESS = "success";

        const int SIZE_OF_BUFFER = 4096;

        // Server program assoicated with this replication manager
        private ServerProgram thisServer;

        /// <summary>
        /// Main constructor for initalization for the replication manager. It will decide whether to start 
        /// listening or not depdening on replica being primary or not as well as send initial requests
        /// for backup replicas.
        /// </summary>
        /// <param name="replica"></param>
        /// <param name="primaryServerIPAddress"></param>
        public ReplicationManager(ServerProgram replica)
        {
            //
            thisServer = replica;
            udpBroadcast.EnableBroadcast = true;

            // Broadcast to local network trying to find if a primary exists or not.
            // Start Listening for udp broadcast messages
            new Thread(() =>
            {
                while(true)
                {
                    StartListeningUdp();
                }
            }).Start();

            // TODO: Send multiple times for udp
            timerForFindingPrimary = new Timer(timerCallBackForFindingPrimary, "isPrimary", 5000, Timeout.Infinite);
            for (int i = 0; i < 3; i++)
            {
                Broadcast("isPrimary");
            }

            // Run listening on its own thread
            new Thread(() =>
            {
                ListenReplica();
            }).Start();
  
        }

        /// <summary>
        /// This method is used to initialize replication depedning on whether it's a server.
        /// </summary>
        /// <param name="isServerPrimary">A bool for whether server is primary or not.</param>
        public void InitializeReplication(bool isServerPrimary)
        {
            if (!isServerPrimary)
            {
                if (!allReplicaAddr.Exists(e => e.Item1.Equals(primaryServerIp)))
                { 
                    // Add Primary server ip address to replica
                    //TODO dont need this, get list update from primary
                    allReplicaAddr.Add(new Tuple<IPAddress, bool>(primaryServerIp, true));

                    // Timer for checking if primary is there
                    timerForCheckingPrimaryExistence = new Timer(CheckServerExistence, "Some state", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

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
        /// This method is responsible for taking a socket connection and reciving incoming message parse it and then send a response.
        /// </summary>
        /// <param name="sock">Socket that was listened on</param>
        public void EstablishConnection(Socket sock)
        {
            Console.WriteLine("Establishing Connection with {0} {1}", (sock.RemoteEndPoint as IPEndPoint).Address, (sock.LocalEndPoint as IPEndPoint).Address);

            // S
            StringBuilder sb = new StringBuilder();

            byte[] buffer = new byte[SIZE_OF_BUFFER];
            int bytesRead = sock.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            Console.WriteLine("Message that was listened to {0}", sb.ToString());

            string requestMessage = sb.ToString().Trim().ToLower();

            byte[] responseMessageForBackupOrCheck = new byte[1024];

            if (requestMessage.StartsWith(REQ_CHECK) || requestMessage.StartsWith(REQ_BACKUP))
            {
                responseMessageForBackupOrCheck = parseRequestMessageForPrimary(requestMessage);
            }
            // Here we want to send back to all backups
            if ((requestMessage.StartsWith(REQ_NAMES)
                || requestMessage.StartsWith(REQ_GAMESESSIONS)
                || requestMessage.StartsWith(REQ_QUEUE))
                && thisServer.isPrimaryServer)
            {
                // TODO: how does socket differ from tcp client.
                sock.Close();

                // Send back to all backups the new updated information
                IEnumerable<IPAddress> IEnumerableOfBackUpIPs = allReplicaAddr.Select(tuple => tuple.Item1);

                // Send all backups updated info
                foreach (IPAddress backupIP in IEnumerableOfBackUpIPs)
                {
                    if (!thisServer.ipAddr.Equals(backupIP))
                    {
                        // Get appeopraite response
                        byte[] responseMessage = parseRequestMessageForPrimary(requestMessage);

                        TcpClient primaryClientToBackup = new TcpClient();
                        primaryClientToBackup.Connect(backupIP, 8000);

                        Console.WriteLine("Sending to every backup this {0}", responseMessage);

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
                }
            }
            // Messages receivied from primary by backup after listening
            else if ((requestMessage.StartsWith(REQ_NAMES)
                    || requestMessage.StartsWith(REQ_GAMESESSIONS)
                    || requestMessage.StartsWith(REQ_QUEUE))
                    && !thisServer.isPrimaryServer)
            {
                Console.WriteLine("Received messages from primary of this type {0}", requestMessage);

                // Update information for backup 
                // Here we are parsing request but since method is same use same
                // TODO: Update names
                parseResponseMessageForBackup(requestMessage);

                // TODO: Response of success

                // TODO: how does socket differ from tcp client.

                sock.Close();

            }
            else
            {
                sock.Send(responseMessageForBackupOrCheck);

                sock.Close();
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
                    Console.WriteLine("Add Replica IP {0} to Server {1}", ipAddr, primaryServerIp);

                    // Add backup ip address to primary server list
                    allReplicaAddr.Add(new Tuple<IPAddress, bool>(ipAddr, true));

                    // Create a response back to the replicationManager of the backup server
                    // add required information to be sent back
                    responseMessage = RES_ADDRESSES + " ";

                    // Send backup servers ip addresses starting from first backup server exculding primary server
                    for (int i = 1; i < allReplicaAddr.Count; i++)
                    {
                        // Comma shouldn't be added at the end of the message
                        if (i != allReplicaAddr.Count - 1)
                        {
                            responseMessage += allReplicaAddr[i].Item1 + ",";
                        }
                        else
                        {
                            responseMessage += allReplicaAddr[i].Item1;
                        }

                    }

                    ASCIIEncoding asen = new ASCIIEncoding();

                    b = asen.GetBytes(responseMessage + "\n\n");
                }
            }
            else if (requestType == REQ_NAMES || requestType == REQ_GAMESESSIONS || requestType == REQ_QUEUE)
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


            if (responseType == RES_ADDRESSES)
            {
                // get IP Addresses of all the other programs 
                string[] arrayOfIPAddresses = messageParam.Split(',');

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
                        // Add tempIP into the list of existing ip addresses
                        if (allReplicaAddr.All(tuple => !tuple.Item1.Equals(ipAddr)))
                        {
                            Console.WriteLine("Add this IP Address to the list {0}", ipAddr);
                            allReplicaAddr.Add(new Tuple<IPAddress, bool>(ipAddr, true));
                        }

                    }
                }
            }
            else if (responseType == REQ_NAMES || responseType == REQ_GAMESESSIONS || responseType == REQ_QUEUE)
            {
                if (!string.IsNullOrEmpty(messageParam))
                {
                    ParseServerResponseMessageToBackUpForGameInfo(responseType, messageParam);
                }
                
            }

        }


        /// <summary>
        /// This method is used to parse reponse messages after sending requests. 
        /// </summary>
        /// <param name="reposnseMessage">Response messages</param>
        private void parseResponseMessageForPrimary(string reposnseMessage)
        {
            throw new NotImplementedException();
        }

        private string ConstructPrimaryMessageToBackupBasedOnRequestType(string requestType)
        {
            // Add response Type
            string responseMessage = string.Empty;
            List<string> names = thisServer.GetPlayerNames();
            GameSession[] sessions = thisServer.GetGameSession();
            List<ConcurrentQueue<ClientInfo>> clientsWaitingForgame = new List<ConcurrentQueue<ClientInfo>>();

            // based on request get the server info needed
            if (requestType.Equals(RES_ADDRESSES))
            {
                Console.WriteLine("ERROR in ConstructPrimaryMessageToBackupBasedOnRequestType");
            }
            else if (requestType.Equals(REQ_NAMES))
            {
                responseMessage = ConstructPrimaryMessageNames(names);
            }
            else if (requestType.Equals(REQ_GAMESESSIONS))
            {
                responseMessage = ConstructPrimaryMessageSession(sessions);
            }
            else if (requestType.Equals(REQ_QUEUE))
            {
                responseMessage = ConstructPrimaryMessageMatch(clientsWaitingForgame);
            }
            
            return responseMessage;
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
        private string ConstructPrimaryMessageNames(List<string> names)
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


        private string ConstructPrimaryMessageMatch(List<ConcurrentQueue<ClientInfo>> clientsWaitingForGame)
        {
            string responseMessage = "queue" + " ";

            // send client names on the server
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                for (int j = 0; j < clientsWaitingForGame[i].Count; j++)
                {
                    if (j.Equals(0))
                    {
                        responseMessage += i + clientsWaitingForGame[i].ElementAt(j).ToMessage() + " ";
                    }
                    else if (j.Equals(clientsWaitingForGame[i].Count - 1))
                    {
                        responseMessage += clientsWaitingForGame[i].ElementAt(j).ToMessage() + ",";
                    }
                    else
                    {
                        responseMessage += clientsWaitingForGame[i].ElementAt(j).ToMessage() + " ";
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
                List<string> tempPlayerNames = new List<string>();

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
                    string[] arrayOfSpecificSession = messageParam.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
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
                        ClientInfo player = new ClientInfo(null);
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
                        player.IPAddr = IPAddress.Parse(arrayOfGameSessionAndPlayerSpecificInfo[0 + extraIndexForGameID]);

                        player.ListeningPort = int.Parse(arrayOfGameSessionAndPlayerSpecificInfo[1 + extraIndexForGameID]);

                        player.PlayerName = arrayOfGameSessionAndPlayerSpecificInfo[2 + extraIndexForGameID];

                        player.PlayerId = int.Parse(arrayOfGameSessionAndPlayerSpecificInfo[3 + extraIndexForGameID]);
                        //}

                        // After extracting gameID, index goes back to zero.
                        extraIndexForGameID = 0;


                        players.Add(player);

                        
                    }

                    // Add to the gamesession
                    gameSession.SetPlayers = players;
                }

                // Add to game session of server
                thisServer.SetGameSession(tempGameSession.ToArray());
            }
            else if (responseType.Equals(REQ_QUEUE))
            {
                // Split game queue by delimiter comma
                string[] arrayOfGameQueues = messageParam.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                List<ConcurrentQueue<ClientInfo>> tempQueues = new List<ConcurrentQueue<ClientInfo>>();

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
                        gameCapacity = int.Parse(arrayOfGameQueues[0]);
                        // TODO: TryParse
                        for (int i = tempQueues.Count; i <= gameCapacity; i++)
                        {
                            tempQueues.Add(new ConcurrentQueue<ClientInfo>());
                        }
                    }

                    for (int i = 1; i < arrayOfGameQueue.Count(); i += 4)
                    {
                        ClientInfo player = new ClientInfo(null);
                        // For every four enteries get the relvent info
                        player.IPAddr = IPAddress.Parse(arrayOfGameQueue[i]);
                        player.ListeningPort = int.Parse(arrayOfGameQueue[i + 1]);
                        player.PlayerId = int.Parse(arrayOfGameQueue[i + 2]);
                        player.PlayerName = arrayOfGameQueue[i + 3];

                        tempQueues[gameCapacity].Enqueue(player);
                    }


                    // After extracting gameID, index goes back to zero.
                    extraIndexForGameCapacity = 0;

                }

                thisServer.SetClientsWaitingForGame(tempQueues);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="replica"></param>
        public void addReplica(ServerProgram replica)
        {
            bool isOnline = true;

            allReplicaAddr.Add(new Tuple<IPAddress, bool>(replica.ipAddr, isOnline));
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
                        SendFromReplicaToServerAndParseResponse(arrayOfReplicaMessages[i]);
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
                }
                catch (SocketException)
                {
                    // In this case: server must have crashed
                    // take over and become the primary 
                    // TODO: This won't work for multiple servers
                    if (allReplicaAddr[1].Item1.ToString() == thisServer.ipAddr.ToString())
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
            else if (replicaMsg.StartsWith(REQ_QUEUE))
            {
                messageToBeSent = "queue" + " ";
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

            // Initalize a new TcpClient
            replicaClient = new TcpClient();

            // will send a message to the primary server
            replicaClient.Connect(primaryServerIp, 8000);

            Stream stm = replicaClient.GetStream();

            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] ba = asen.GetBytes(messageToBeSent);

            stm.Write(ba, 0, ba.Length);
            byte[] bb = new byte[SIZE_OF_BUFFER];

            // Receive response from primary
            int k = stm.Read(bb, 0, SIZE_OF_BUFFER);

            string responseMessage = "";
            char c = ' ';
            for (int i = 0; i < k; i++)
            {
                c = Convert.ToChar(bb[i]);
                responseMessage += c;
            }

            // Prepare another response to backups
            parseResponseMessageForBackup(responseMessage);

            // TODO:

            replicaClient.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tempMsg"></param>
        public void SendFromServerToBackUPSWhenStateChanges(string updateType)
        {
            // Construct a message to be sent based on type of update
            string messageUpdate = ConstructPrimaryMessageToBackupBasedOnRequestType(updateType);

            // Send back to all backups the new updated information
            IEnumerable<IPAddress> IEnumerableOfBackUpIPs = allReplicaAddr.Select(tuple => tuple.Item1);

            // Send all backups updated info
            foreach (IPAddress backupIP in IEnumerableOfBackUpIPs)
            {
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
                Socket sock = rmListener.AcceptSocket();
                new Thread(() => {
                    EstablishConnection(sock);
                }).Start();
            }
        }

        public void MakeThisServerPrimary()
        {
            thisServer.isPrimaryServer = true;
            // TODO: change this to try Parse
            // primaryServerIp = IPAddress.Parse("162.246.157.120");
            thisServer.StartListen();
            timerForCheckingPrimaryExistence.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public bool IsPrimary()
        {
            return thisServer.isPrimaryServer;
        }

        /// <summary>
        /// This method checks that the primary server exists at all time.
        /// </summary>
        /// <param name="state">This parameter has to be passed even though we don't need it here.</param>
        private void CheckServerExistence(object state)
        {
            // Send to primary a message
            lock (thisLock)
            {
                DecideOnMessagesSendFromBackUpToServer(false);
            }
        }

        /// <summary>
        /// This method will broadcast a message to all ip addresses in the local newtork.
        /// </summary>
        /// <param name="message">Message to be broadcasted to all local network peers.</param>
        private void Broadcast(string message)
        {
            // Initialize a new udp client
            UdpClient client = new UdpClient(AddressFamily.InterNetwork);
            client.EnableBroadcast = true;

            // Send a request message asking if primary exists.
            byte[] bytes = Encoding.ASCII.GetBytes(message);

            // Send message
            client.Send(bytes, bytes.Length, sendingIP);

            Console.WriteLine("I sent {0}", message);

            // Close client
            client.Close();
        }

        // <summary>
        // this method will start listening for incoming requests to check if replica is primary or not
        // </summary>
        private void StartListeningUdp()
        {
            //receive messages
            byte[] bytes = udpBroadcast.Receive(ref receivingIP);
            string message = Encoding.ASCII.GetString(bytes);
            Console.WriteLine("I received {0}", message);
            // todo: disable sending messages to yourself by default
            if (!receivingIP.Address.Equals(thisServer.ipAddr)) ParseBroadcastMessages(message, receivingIP);
        }

        /// <summary>
        /// This method will parse incoming requests that are sent using broadcase udp.
        /// </summary>
        /// <param name="receivedMessage">Message to be parsed</param>
        private void ParseBroadcastMessages(string receivedMessage, IPEndPoint ip)
        {
            // Parse message received 
            if (receivedMessage.StartsWith("isPrimary"))
            {
                // Check if this backup server is primary
                if (IsPrimary())
                {
                    // Send a response back
                    // TODO: Only send to specific ip.
                    // Don't broadcast 
                    Broadcast("primary");
                }
            }
            else if (receivedMessage.StartsWith("primary"))
            {
                // Disable timer 
                timerForFindingPrimary.Change(Timeout.Infinite, Timeout.Infinite);

                // Make this server a backup
                thisServer.isPrimaryServer = false;

                // Take the ip address of 
                primaryServerIp = ip.Address;

                InitializeReplication(false);
            }
        }

        /// <summary>
        /// This method is a callback for a timer where it's being called when a server doesn't get any reply when it's initialized.
        /// The server becomes the primary server when that happens.
        /// </summary>
        /// <param name="state">Passed parameter to the call back -> Object</param>
        private void timerCallBackForFindingPrimary(object state)
        {
            thisServer.isPrimaryServer = true;
            primaryServerIp = thisServer.ipAddr; 

            Console.WriteLine("I'm primary");
            addReplica(thisServer);

            thisServer.StartListen();
            
        }

    }
}
