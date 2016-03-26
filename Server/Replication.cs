﻿using System;
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
        Timer timerForChecking;
        // Timer for 
        Timer timerForFindingPrimary;
        // lock object for check messages so it won't continue sending messages on different threads
        private Object thisLock = new Object();
        private Object udpLock = new Object();
        // Requests to be sent from replica to primary server every time a new replica is initalized.
        public static readonly string[] arrayOfReplicaMessages = { "backup", "name", "session" };
        // Udp client listening for broadcast messages
        private readonly UdpClient udpBroadcast = new UdpClient(15000);
        // IP Address for broadcasting
        IPEndPoint sendingIP = new IPEndPoint(IPAddress.Broadcast, 15000);
        IPEndPoint receivingIP = new IPEndPoint(IPAddress.Any, 0);

        // Request messsages between replicas and server
        const string REQ_REPLICA = "backup";
        const string RES_ADDRESSES = "address";
        const string REQ_NAMES = "name";
        const string REQ_GAMESESSIONS = "session";
        const string REQ_CHECK = "check";
        const string RESP_SUCCESS = "success";

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
                // Add Primary server ip address to replica
                //TODO dont need this, get list update from primary
                //allReplicaAddr.Add(new Tuple<IPAddress, bool>(primaryServerIp, true));

                // Timer for checking if primary is there
                timerForChecking = new Timer(CheckServerExistence, "Some state", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                // secondary replica sends a replica request
                DecideOnMessagesSendFromBackUpToServer(true);
                
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

            byte[] buffer = new byte[2048];
            int bytesRead = sock.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            Console.WriteLine("Message that was listened to {0}", sb.ToString());

            string requestMessage = sb.ToString().Trim().ToLower();

            // Here we want to send back to all backups
            if ((requestMessage.StartsWith(REQ_REPLICA) 
                || requestMessage.StartsWith(REQ_NAMES)
                || requestMessage.StartsWith(REQ_GAMESESSIONS))
                && thisServer.isPrimaryServer)
            {
                // Get appeopraite response
                byte[] responseMessage = parseRequestMessageForPrimary(requestMessage);

                // Send back to all backups the new updated information
                IEnumerable<IPAddress> IEnumerableOfBackUpIPs = allReplicaAddr.Select(tuple => tuple.Item1);

                // Send all backups updated info
                foreach (IPAddress backupIP in IEnumerableOfBackUpIPs)
                {
                    TcpClient primaryClientToBackup = new TcpClient();
                    primaryClientToBackup.Connect(backupIP, 8000);

                    Console.WriteLine("Sending to every backup this {0}", requestMessage);

                    Stream stm = primaryClientToBackup.GetStream();

                    ASCIIEncoding asen = new ASCIIEncoding();

                    stm.Write(responseMessage, 0, responseMessage.Length);
                    byte[] responseOfBackUpToServerResponse = new byte[4096];

                    // Receive response from primary
                    int k = stm.Read(responseOfBackUpToServerResponse, 0, 4096);

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
            // Messages receivied from primary by backup after listening
            else if ((requestMessage.StartsWith(REQ_REPLICA)
                    || requestMessage.StartsWith(REQ_NAMES)
                    || requestMessage.StartsWith(REQ_GAMESESSIONS))
                    && !thisServer.isPrimaryServer)
            {
                Console.WriteLine("Received messages from primary of this type {0}", requestMessage);

                // Update information for backup 
                // Here we are parsing request but since method is same use same
                // TODO: Update names
                parseResponseMessageForBackup(requestMessage);

                // TODO: Response of success
            }

            //sock.Send(responseMessage);


            // TODO: how does socket differ from tcp client.
            sock.Close();

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
            byte[] b = new byte[4096];

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
            byte[] b = new byte[4096];

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
            if (requestType == REQ_REPLICA)
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
            else if (requestType == REQ_NAMES || requestType == REQ_GAMESESSIONS)
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
            else if (responseType == REQ_NAMES || responseType == REQ_GAMESESSIONS)
            {
                if (!string.IsNullOrEmpty(messageParam))
                {
                    ParseServerResponseMessageToBackUpForGameInfo(responseType, messageParam);
                }
                else
                {
                    Console.WriteLine("There was a mistake in the response message coming from primary to backup about player names which is: {0}", messageParam);
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
            List<string> names = thisServer.allPlayerNamesUsed;
            Dictionary<string, List<string>> session = thisServer.gameSession;

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
                responseMessage = ConstructPrimaryMessageSession(session);
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
                    responseMessage += thisServer.allPlayerNamesUsed[i] + ",";
                }
                else
                {
                    responseMessage += thisServer.allPlayerNamesUsed[i];
                }
            }

            return responseMessage;
        }

        /// <summary>
        /// This method constructs a message that will be sent from primary to replica for session request.
        /// </summary>
        /// <param gameSessionInfo="gameSessionInfo">Dictionary of gameIDs to list of player info to be sent from primary server to replica.</param>
        /// <returns>Message to be sent to the replica</returns>
        public string ConstructPrimaryMessageSession(Dictionary<string, List<string>> gameSessionInfo)
        {
            string responseMessage = "session" + " ";
            int counter = 0;

            // send client names on the server
            foreach (KeyValuePair<string, List<string>> idToPlayerInfo in gameSessionInfo)
            {
                responseMessage += idToPlayerInfo.Key + " ";


                // append all players info to response message
                for (int j = 0; j < idToPlayerInfo.Value.Count; j++)
                {
                    if (j != idToPlayerInfo.Value.Count() - 1)
                    {
                        responseMessage += idToPlayerInfo.Value[j] + ",";
                    }
                    else
                    {
                        responseMessage += idToPlayerInfo.Value[j];
                    }
                }

                // Append a newline at the end
                if (counter != gameSessionInfo.Count - 1)
                {
                    responseMessage += "\n";
                }

                counter++;
            }

            // Append new lines for end of message
            responseMessage += "\n\n";

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

                // Convert IP address from string to IPAddress
                foreach (string tempName in arrayOfPlayerNames)
                {
                    // Add tempIP into the list of existing ip addresses
                    if (thisServer.allPlayerNamesUsed.All(name => !name.Equals(tempName)))
                    {
                        thisServer.allPlayerNamesUsed.Add(tempName);
                    }
                }
            }

            else if (responseType == REQ_GAMESESSIONS)
            {
                // split games sessions
                string[] arrayOfSessions = messageParam.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);


                // Convert IP address from string to IPAddress
                foreach (string tempSession in arrayOfSessions)
                {
                    // Split each game session by comma serperator
                    string[] arrayOfSpecificSession = messageParam.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    List<string> playersInfo = new List<string>();
                    string gameID = "";

                    // Use an integer to differ between string with gameID and without
                    // First info will contain a game ID
                    int extraIndexForGameID = 1;

                    // Extract Game ID and players Info
                    for (int gameSessionAndPlayerInfoIndex = 0; gameSessionAndPlayerInfoIndex < arrayOfSpecificSession.Count(); gameSessionAndPlayerInfoIndex++)
                    {
                        // Split speicific info by spaces
                        string[] arrayOfGameSessionAndPlayerSpecificInfo = arrayOfSpecificSession[gameSessionAndPlayerInfoIndex].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries); ;

                        string playerIP = string.Empty;
                        string playerPort = string.Empty;
                        string playerName = string.Empty;
                        string playerID = string.Empty;

                        if (extraIndexForGameID == 1)
                        {
                            gameID = arrayOfGameSessionAndPlayerSpecificInfo[0];
                        }

                        // Check that the gameSession doesn't alreay exist on this backup server
                        if (!thisServer.gameSession.ContainsKey(gameID))
                        {
                            playerIP = arrayOfGameSessionAndPlayerSpecificInfo[0 + extraIndexForGameID];

                            playerPort = arrayOfGameSessionAndPlayerSpecificInfo[1 + extraIndexForGameID];

                            playerName = arrayOfGameSessionAndPlayerSpecificInfo[2 + extraIndexForGameID];

                            playerID = arrayOfGameSessionAndPlayerSpecificInfo[3 + extraIndexForGameID];
                        }

                        // After extracting gameID, index goes back to zero.
                        extraIndexForGameID = 0;

                        string playerInfoDelimitedByComma = playerIP + " " + playerPort + " " + playerName + " " + playerID;
                        playersInfo.Add(playerInfoDelimitedByComma);

                        
                    }

                    // Add to the gamesession
                    thisServer.gameSession[gameID] = playersInfo;
                }
                
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
                    for (int i = 0; i < 3; i++)
                    {
                        SendFromReplicaToServerAndParseResponse(arrayOfReplicaMessages[i]);
                    }

                    // Print 
                    //foreach(string tempStr in thisServer.allPlayerNamesUsed)
                    //{
                    //    Console.WriteLine("player name is {0} ", tempStr);
                    //}

                    //foreach (KeyValuePair<string, List<string>> tempDict in thisServer.gameSession)
                    //{
                    //    Console.WriteLine("The game ID is {0} ", tempDict.Key);

                    //    foreach (string tempString in tempDict.Value)
                    //    {
                    //        Console.WriteLine("The player infor {1}", tempString);
                    //    }
                    //}

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
            else if (replicaMsg.StartsWith(REQ_REPLICA))
            {
                // Message to be sent 
                messageToBeSent = "replica" + " " + thisServer.ipAddr;
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
            byte[] bb = new byte[4096];

            // Receive response from primary
            int k = stm.Read(bb, 0, 4096);

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
            timerForChecking.Change(Timeout.Infinite, Timeout.Infinite);
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
