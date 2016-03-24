using System;
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
        public static readonly string[] arrayOfReplicaMessages = { "replica", "name", "session" };
        // Udp client listening for broadcast messages
        private readonly UdpClient udpBroadcast = new UdpClient(15000);
        // IP Address for broadcasting
        IPEndPoint sendingIP = new IPEndPoint(IPAddress.Broadcast, 15000);
        IPEndPoint receivingIP = new IPEndPoint(IPAddress.Any, 0);

        // Request messsages between replicas and server
        const string REQ_REPLICA = "replica";
        const string REQ_ADDRESS = "address";
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
                while (true)
                {
                    StartListeningUDP();
                }
            }).Start();

            // TODO: Send multiple times for udp
            timerForFindingPrimary = new Timer(timerCallBackForFindingPrimary, "isPrimary", 10000, Timeout.Infinite);
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
                allReplicaAddr.Add(new Tuple<IPAddress, bool>(primaryServerIp, true));

                // Timer for checking if primary is there
                timerForChecking = new Timer(CheckServerExistence, "Some state", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                // secondary replica sends a replica request
                SendReplica(true);
                
            }
            else
            {
                addReplica(thisServer);

                // Make this server start listening
                thisServer.StartListen();
            }
        }

        /// <summary>
        /// This method is responsible for taking a socket connection and reciving coming message parse it and then send a response.
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

            byte[] responseMessage = parseRequestMessage(requestMessage);

            // Print messages

            sock.Send(responseMessage);

            sock.Close();

        }

        /// <summary>
        /// This method takes a string that was sent through the network and parses it and return a respons to it.
        /// </summary>
        /// <param name="requestMessage">This parameter contains what was sent through the network.</param>
        /// <returns>response message in bytes array.</returns>
        private byte[] parseRequestMessage(string requestMessage)
        {
            string requestType;
            string messageParam;
            string responseMessage = string.Empty;
            bool parsedCorrectly = false;
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

            if (requestType == REQ_REPLICA)
            {
                // get IP Address of the replica from message parameters
                string ipAddressString = messageParam;

                // Convert IP address from string to IPAddress
                IPAddress ipAddr;
                if (!IPAddress.TryParse(ipAddressString, out ipAddr))
                {
                    // In case what was sent can't be parsed as an IP address
                    // TODO: deal with this error in some way
                    Console.WriteLine("ERROR");
                    parsedCorrectly = false;
                }
                else
                {
                    // add information about this replica
                    Console.WriteLine("Add Replica IP to Server {0}", ipAddr);
                    allReplicaAddr.Add(new Tuple<IPAddress, bool>(ipAddr, true));

                    // Create a response back to the replicationManager of the replica
                    // add required information to be sent back
                    responseMessage = REQ_ADDRESS + " ";

                    // Send replicas ip addresses starting from first replica
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
            
            else if (requestType == REQ_ADDRESS)
            {
                // get IP Addresses of all the other programs 
                string[] arrayOfIPAddresses = messageParam.Split(',');

                // 
                foreach (string tempstr in arrayOfIPAddresses)
                {
                    Console.WriteLine("parsing info with IP = {0}", tempstr);
                }
                Console.WriteLine("");

                // Convert IP address from string to IPAddress
                foreach (string tempIP in arrayOfIPAddresses)
                {
                    IPAddress ipAddr;
                    if (!IPAddress.TryParse(tempIP, out ipAddr))
                    {
                        // In case what was sent can't be parsed as an IP address
                        // TODO: deal with this error in some way
                        Console.WriteLine("ERROR");
                        parsedCorrectly = false;
                    }
                    else
                    {
                        // Add tempIP into the list of existing ip addresses
                        if (allReplicaAddr.All(tuple => !tuple.Item1.Equals(ipAddr)))
                        {
                            Console.WriteLine("Add this IP Address to the list {0}", ipAddr);
                            allReplicaAddr.Add(new Tuple<IPAddress, bool>(ipAddr, true));
                        }
                        parsedCorrectly = true;
                    }
                }

                if (parsedCorrectly)
                {
                    // Create a response back to the replicationManager of the server
                    responseMessage = RESP_SUCCESS + " " + REQ_ADDRESS + "  ";

                    ASCIIEncoding asen = new ASCIIEncoding();

                    b = asen.GetBytes(responseMessage + "\n\n");
                }

                foreach (Tuple<IPAddress, bool> replica in allReplicaAddr)
                {
                    Console.WriteLine("The replicas are: {0} {1}", replica.Item1, replica.Item2);
                }
            }
            else if (requestType == REQ_NAMES || requestType == REQ_GAMESESSIONS)
            {
                if (thisServer.isPrimaryServer)
                {
                    responseMessage = ConstructPrimaryMessagesBasedOnType(requestType);
                }
                else
                {
                    if (!string.IsNullOrEmpty(messageParam))
                    {
                        responseMessage = ConstructReplicaMessageAfterReceivingServerInfo(requestType, messageParam);
                    }
                }

                ASCIIEncoding asen = new ASCIIEncoding();

                b = asen.GetBytes(responseMessage + "\n\n");
            }
            /*else if (requestType == RESP_SUCCESS)
            {
                string success_message = messageParam.Substring(0, messageParam.IndexOf(" ")).Trim();

                if (success_message.StartsWith(REQ_REPLICA))
                {
                    allReplicaAddr.Add(new Tuple<IPAddress, bool>(thisServer.ipAddr, true));
                }
            }*/

            return b;
        }

        private string ConstructPrimaryMessagesBasedOnType(string requestType)
        {
            // Add response Type
            string responseMessage = string.Empty;
            List<string> names = thisServer.allPlayerNamesUsed;
            Dictionary<string, List<string>> session = thisServer.gameSession;

            // based on request get the server info needed
            if (requestType.Equals(REQ_ADDRESS))
            {
                Console.WriteLine("This is where it used to be info");
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

                // get player information
                // string[] arrayOfPlayerInfo = idToplayerInfo.Value.Split(',');

                // append all player info to response message
                for (int j = 0; j < idToPlayerInfo.Value.Count; j++)
                {
                    if (j != idToPlayerInfo.Value.Count() - 1)
                    {
                        responseMessage += idToPlayerInfo.Value[j] + " ";
                    }
                    else
                    {
                        responseMessage += idToPlayerInfo.Value[j];
                    }
                }

                // Append a comma at the end
                if (counter != gameSessionInfo.Count - 1)
                {
                    responseMessage += ",";
                }

                counter++;
            }

            return responseMessage;
        }

        /// <summary>
        /// This method constructs a message that will be sent from primary to replica for name request.
        /// </summary>
        /// <param name="names">List of names of players to be sent from primary server to replica.</param>
        /// <returns>Message to be sent to the replica</returns>
        public string ConstructReplicaMessageAfterReceivingServerInfo(string requestType, string messageParam)
        {
            string responseMessage = string.Empty;

            if (requestType == REQ_NAMES)
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
            else if (requestType == REQ_GAMESESSIONS)
            {
                // split games sessions
                string[] arrayOfSessions = messageParam.Split(',');

                

                // Convert IP address from string to IPAddress
                foreach (string tempSession in arrayOfSessions)
                {
                    List<string> playersInfo = new List<string>();

                    // Extract game ID
                    string gameID = new string(tempSession
                   .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray());

                    // Add session into existing sessions if it doesn't exist
                    if (!thisServer.gameSession.ContainsKey(gameID))
                    {
                        string playerIP = new string(tempSession
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray());

                        string playerPort = new string(tempSession
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray());

                        string playerName = new string(tempSession
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray());

                        string playerID = new string(tempSession
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .SkipWhile(ch => !char.IsWhiteSpace(ch))
                        .SkipWhile(ch => char.IsWhiteSpace(ch))
                        .TakeWhile(ch => !char.IsWhiteSpace(ch)).ToArray());

                        string playerInfoDelimitedByComma = playerIP + " " + playerPort + " " + playerName + " " + playerID;
                        playersInfo.Add(playerInfoDelimitedByComma);

                        // TODO: have a loop before to add all players
                        thisServer.gameSession[gameID] = playersInfo;
                    }
                }
            }

            return responseMessage;
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
        /// Communication between each replica and the server. Replicas will send either replica or check.
        /// </summary>
        /// <param name="tempMsg">what replicas are trying to send as clients</param>
        public void SendReplica(bool isReplica)
        {

            if (isReplica)
            {

                // Catch errors in case we are checking server existence
                try
                {
                    // Loop through three requests for duplicating data
                    for (int i = 0; i < 3; i++)
                    {
                        ConnectReplica(arrayOfReplicaMessages[i]);
                    }

                    // Print 
                    foreach(string tempStr in thisServer.allPlayerNamesUsed)
                    {
                        Console.WriteLine("player name is {0} ", tempStr);
                    }

                    foreach (KeyValuePair<string, List<string>> tempDict in thisServer.gameSession)
                    {
                        Console.WriteLine("The game ID is {0} ", tempDict.Key);

                        foreach (string tempString in tempDict.Value)
                        {
                            Console.WriteLine("The player infor {1}", tempString);
                        }
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
                    ConnectReplica("check");
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

        //Sender
        private void ConnectReplica(string tempMsg)
        {
            string messageToBeSent = ConstructReplicaMessagesFromReplicaToServer(tempMsg);

            // Initalize a new TcpClient
            replicaClient = new TcpClient();

            // will send a message to the replica
            replicaClient.Connect(primaryServerIp, 8000);

            Stream stm = replicaClient.GetStream();

            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] ba = asen.GetBytes(messageToBeSent);

            stm.Write(ba, 0, ba.Length);
            byte[] bb = new byte[4096];
            // Receive response
            int k = stm.Read(bb, 0, 4096);

            string responseMessage = "";
            char c = ' ';
            for (int i = 0; i < k; i++)
            {
                c = Convert.ToChar(bb[i]);
                responseMessage += c;
            }

            parseRequestMessage(responseMessage);

            replicaClient.Close();
        }

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
                SendReplica(false);
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

            // Close client
            client.Close();
        }

        /// <summary>
        /// This method will start Listening for incoming requests to check if replica is primary or not
        /// </summary>
        private void StartListeningUDP()
        {
            // receive messages
            
            byte[] bytes = udpBroadcast.Receive(ref receivingIP);
            string message = Encoding.ASCII.GetString(bytes);

            // TODO: Disable sending messages to yourself by default
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
                    Broadcast("primary");
                }
            }
            else if (receivedMessage.StartsWith("primary"))
            {
                timerForFindingPrimary.Change(Timeout.Infinite, Timeout.Infinite);
                // Make this server a backup
                thisServer.isPrimaryServer = false;

                // Take the ip address of 
                primaryServerIp = ip.Address;

                InitializeReplication(false);
            }
            else 
            {
                timerForFindingPrimary.Change(Timeout.Infinite, Timeout.Infinite);

                thisServer.isPrimaryServer = true;
                primaryServerIp = thisServer.ipAddr;

                InitializeReplication(true);
            }
        }

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
