using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    /// <summary>
    /// Client class where it creates a client to server and peer for the peer-to-peer network.
    /// </summary>
    public class ClientProgram
    {
        TcpClient client;
        private string playerName;
        public bool inGame = false;

        public const string REQ_GAME = "game";
        public const string REQ_PLAYERS = "players";
        public const string REQ_CANCEL = "cancel";
        public const string REQ_CHECKNAME = "checkname";
        public const string REQ_RECONN = "reconn";
        public const string RESP_SUCCESS = "success";
        public const string RESP_FAILURE = "failure";

        public static string SERVER_IP = "";

        // Holds information about other peers in the system: IPAddress, portNumber, name and ID.
        List<Tuple<string, int, string, int, int>> peersInfo;

        /// <summary>
        /// Constructor for creating a client, it will connect to the server and it will have a unique name.
        /// TODO: Change defualt name to something else
        /// </summary>
        /// <param name="player"></param>
        public ClientProgram()
        { 
            // Set IP address of server
            if (SERVER_IP == "")
            {
                Console.Write("NO SERVER IP SET, Enter server ip: ");
                SERVER_IP = Console.ReadLine();
            }

            // Connect to server and set unique player name 
            string pName = String.Empty;
            bool checkNameResult = false;
            do
            {
                connectToServer();
                Console.Write("Enter Your Player Name: ");
                pName = Console.ReadLine();
                pName = pName.Trim().Replace(" ", "").Replace("\t", "");
                checkNameResult = checkNameAvailable(pName);
                if (!checkNameResult)
                {
                    Console.Write("Name exists on server, is it you? (Y/N)");
                    string isityou = Console.ReadLine().Trim().ToLower();

                    if(isityou == "y")
                    {
                        checkNameResult = true;
                    }
                    else
                    {
                        Console.Write("Re");
                    }
                    
                }

            } while (!checkNameResult);

            playerName = pName;

        }

        /// <summary>
        /// This method connects to the server for estiblishing a game.
        /// TODO: Should we have a specific IP and port or we should change it.
        /// </summary>
        private void connectToServer()
        {
           

            bool connected = true;
            do
            {
                client = new TcpClient();
                try
                {
                    connected = true;
                    Console.WriteLine("Connecting to Server.....");

                    // Connect to the server
                    client.Connect(SERVER_IP, 8001);
                }catch (Exception e)
                {
                    connected = false;
                    client.Close();
                }

            } while (!connected);

            
            Console.WriteLine("Connected to Server at IP {0} and port {1}", SERVER_IP, 8001);


        }


        public bool checkNameAvailable(string pName)
        {
            if( SendRequest(REQ_CHECKNAME + " "  + pName) != -1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Communication between server and client method to send requests from clients to the server.
        /// </summary>
        /// <param name="msg"></param>
        public int SendRequest(string msg) 
        {
            
            string reqMessage = msg.Trim();
            string[] reqMessageElem = reqMessage.Split(' ');
            //
            reqMessageElem = reqMessageElem.Where(elem => elem != "").ToArray();
            string req = reqMessageElem[0];
            if (req == REQ_GAME)
            {
                int inttest;
                if (reqMessageElem.Length < 2 || !int.TryParse(reqMessageElem[1], out inttest))
                {
                    Console.WriteLine("USAGE: game <number>");
                    client.Close();
                    if (!inGame)
                    {
                        // Connect back to server immediately if user not in game
                        connectToServer();
                    }
                    return 0 ;
                }
                string numOfPeersToMatch = reqMessageElem[1];

                reqMessage = req +  " " + playerName + " " + numOfPeersToMatch;
            }
            else if(req == REQ_CANCEL)
            {
                reqMessage = req;
            }
            else if (req == REQ_PLAYERS)
            {
                reqMessage = req;
            }
            else if (req == REQ_CHECKNAME)
            {
                reqMessage = msg;
            }
            else if (req == REQ_RECONN)
            {
                int inttest;
                if (reqMessageElem.Length < 2 || !int.TryParse(reqMessageElem[1], out inttest))
                {
                    Console.WriteLine("USAGE: reconn <gameId>");
                    client.Close();
                    if (!inGame)
                    {
                        // Connect back to server immediately if user not in game
                        connectToServer();
                    }
                    return 0;
                }
                string gameId = reqMessageElem[1];

                reqMessage = req + " " + gameId;
            }
            else
            {
                client.Close();
                if (!inGame)
                {
                    // Connect back to server immediately if user not in game
                    connectToServer();
                }
                return 0;
            }

            reqMessage += "\n\n";
            try { 
                Stream stm = client.GetStream();

                ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(reqMessage);

                Console.WriteLine("Transmitting request to the server.....\n");
                stm.Write(ba, 0, ba.Length);
                byte[] bb = new byte[2048];
                Console.WriteLine("Waiting for response from Server...");
                int k = stm.Read(bb, 0, 2048);

                string responseMessage = "";
                char c = ' ';
                for (int i = 0; i < k; i++)
                {
                    c = Convert.ToChar(bb[i]);
                    responseMessage += c;
                }


                if (processResponse(responseMessage) == -1)
                {
                    Console.WriteLine("\nDEBUG: INVALID REQUEST/RESPONSE\n");
                    client.Close();
                    connectToServer();
                    return -1;
                }

                client.Close();
                if (!inGame)
                {
                    // Connect back to server immediately if user not in game
                    connectToServer();
                }
            }
            catch (Exception e)
            {
                client.Close();
                connectToServer();
            }
            
            return 0;
        }

        /// <summary>
        /// This method proccess messages coming from the server according to our design document
        /// </summary>
        /// <param name="responseMessage"></param>
        /// <returns></returns>
        private int processResponse(string responseMessage)
        {

            responseMessage = responseMessage.Trim();

            if (responseMessage.StartsWith(RESP_SUCCESS) /*&& responseMessage.EndsWith("\n\n")*/)
            {
                responseMessage = responseMessage.Substring(RESP_SUCCESS.Length).Trim();

                string requestType = responseMessage.Substring(0, responseMessage.IndexOf(" ")).Trim();
                responseMessage = responseMessage.Substring(requestType.Length);

                Console.WriteLine("\nDEBUG: " + requestType + "\n");
                if (requestType == REQ_GAME)
                {
                    peersInfo = new List<Tuple<string, int, string, int, int>>();
                    IEnumerable<string> temp = responseMessage.Split(',');
                    peersInfo = temp.Where(elem => !string.IsNullOrEmpty(elem)).Select(info =>
                    {
                        string[] peerInfo = info.Trim().Split(' ');
                        Tuple<string, int, string, int, int> t = null;
                        if (!string.IsNullOrEmpty(info))
                        {
                            // TODO: deal with cases when integer can't be parsed
                            t = new Tuple<string, int, string, int, int>(peerInfo[0], int.Parse(peerInfo[1]), peerInfo[2], int.Parse(peerInfo[3]),0); 
                        }

                        return t;
                       
                    }).ToList();

                    inGame = true;
                    return 0;
                }
                else if (requestType == REQ_RECONN)
                {
                    peersInfo = new List<Tuple<string, int, string, int, int>>();
                    IEnumerable<string> temp = responseMessage.Split(',');
                    peersInfo = temp.Where(elem => !string.IsNullOrEmpty(elem)).Select(info =>
                    {
                        string[] peerInfo = info.Trim().Split(' ');
                        Tuple<string, int, string, int, int> t = null;
                        if (!string.IsNullOrEmpty(info))
                        {
                            // TODO: deal with cases when integer can't be parsed
                            t = new Tuple<string, int, string, int, int>(peerInfo[0], int.Parse(peerInfo[1]), peerInfo[2], int.Parse(peerInfo[3]),0);
                        }

                        return t;

                    }).ToList();

                    inGame = true;
                    return 0;
                }
                else if (requestType == REQ_PLAYERS)
                {
                    // DISPLAY playernum ON GUI
                    string playernum = responseMessage;
                    Console.WriteLine("\nNum of Players on server now: " + playernum);
                    return 0;
                }
                else if (requestType == REQ_CANCEL)
                {
                    // INDICATES THAT THE USER HAVE CANCELED 
                    Console.WriteLine("\nYou have CANCELED your match making.");
                    return 0;
                }
                else if (requestType == REQ_CHECKNAME)
                {
                    Console.WriteLine("\nName is available!");
                    return 0;
                }

            }
            else if (responseMessage.StartsWith(RESP_FAILURE))
            {

                responseMessage = responseMessage.Substring(RESP_FAILURE.Length).Trim();
                string requestType = responseMessage.Substring(0, responseMessage.IndexOf(" ")).Trim();
                responseMessage = responseMessage.Substring(requestType.Length);

                Console.WriteLine("\nDEBUG: " + requestType + "\n");

                if (requestType == REQ_CHECKNAME)
                {
                    Console.WriteLine("\nName is not available!");
                    return -1;
                }else if(requestType == REQ_RECONN)
                {
                    Console.WriteLine(responseMessage);
                    return 0;
                }
            }

           

            return -1;

        }

        public void startClient()
        {

            // Continously stay connected to the server
            while (true) { 
                try
                {
                    while (true)
                    {

                        if (!inGame)
                        {

                            Console.Write("Send request (game, players, cancel, reconn): ");
                            var request = Console.ReadLine().Trim().ToLower();
                            if(request != String.Empty) { 

                                Console.WriteLine("Sending request \"{0}\"", request);
                                SendRequest(request);
                            }

                        }

                        else
                        {
                            // Leave the server if a game was matched, 
                            //   proceed to p2p connection with other players
                            break;
                        }
                    }

                    using (Peer peer = new Peer(playerName, peersInfo))
                    
                    // Game ended connect back to server
                    inGame = false;
                    connectToServer();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    client.Close();
                    Console.Error.WriteLine(e.StackTrace);
                };
             }
            
        }

        static void Main(string[] args)
        {
            try
            {
                

                //Console.Write("Enter How many people you want to play with: ");
                // TODO: Add code to deal with cases when user enter something other than a int
                //string numberOfPeers = Console.ReadLine();
                ClientProgram aClient = new ClientProgram();
                aClient.startClient();


                Console.Write("--Program terminated. See you next time!--");
                Console.Read();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }
    }
}
