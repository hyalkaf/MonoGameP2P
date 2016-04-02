using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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

        private class Request
        {
            public const string GAME = "game";
            public const string PLAYERS = "players";
            public const string CANCEL = "cancel";
            public const string CHECKNAME = "checkname";
            public const string RECONN = "reconn";
            public const string SERVRECONN = "servreconn";
        }

        private class Response
        {
            public const string SUCCESS = "success";
            public const string FAILURE = "failure";
            public const string ERROR = "error";
        }

        enum GameConnectType
        {
           NewGame,Reconnect 
        }
        enum ResponseStatus
        {
            Ok,Game,Error
        }

        private GameConnectType connectType;
        public const int SERVER_PORT = 8001;

        private TcpClient client;

        private string playerName;
        public bool inGame = false;

        public static string SERVER_IP = "";

        // For interrupting user input
        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        const int VK_RETURN = 0x0D;
        const int WM_KEYDOWN = 0x100;


        // Holds information about other peers in the system: IPAddress, portNumber, name and ID.
        List<PeerInfo> allPeersInfo;
       

        /// <summary>
        /// Constructor for creating a client, it will connect to the server and prompts for unique name.
        /// </summary>
        public ClientProgram()
        {
            // Set IP address of server
            if (SERVER_IP == "")
            {
                Console.Write("NO SERVER IP SET, Enter server ip: ");
                SERVER_IP = Console.ReadLine();
            }

            Login();

        }

        private void Login()
        {
            // Connect to server and set unique player name 
            string pName = String.Empty;
            int checkNameResult = -1;

            do
            {
                ConnectToServer();

                Console.Write("Enter Your Player Name: ");
                pName = Console.ReadLine();
                pName = pName.Replace(" ", "").Replace("\t", "");

                checkNameResult = SendRequest(Request.CHECKNAME + " " + pName);
                if (checkNameResult == -1)
                {
                    Console.Write("Name exists on server, is it you? (Y/N)");
                    string isityou = Console.ReadLine().Trim().ToLower();

                    if (isityou == "y")
                    {
                        checkNameResult = 0;
                        SendRequest(Request.SERVRECONN + " " + pName);
                    }
                    else
                    {
                        Console.Write("Re");
                    }

                }

            } while (checkNameResult == -1);

            playerName = pName;
        }

        /// <summary>
        /// This method connects to the server for estiblishing a game.
        /// TODO: Should we have a specific IP and port or we should change it.
        /// </summary>
        private void ConnectToServer(bool reqServReconn = false)
        {

            if (!inGame) { 
                bool connected = true;
                int tryTimes = 10;
                do
                {
                    client = new TcpClient();
             
                    try
                    {
                        connected = true;
                        Console.WriteLine("Connecting to Server.....");

                        // Connect to the server
                        client.ConnectAsync(SERVER_IP, SERVER_PORT).Wait(5000);
                    }
                    catch (Exception)
                    {
                        reqServReconn = true;
                        Console.WriteLine("Retrying...");
                        connected = false;
                        tryTimes--;
                        client.Close();
                        if (tryTimes < 1)
                        {
                            Console.Write("Did you have the wrong Server IP Address? (Press Enter or enter new IP Address: )");
                            string result = Console.ReadLine();

                            IPAddress newip;
                            if(result.Trim() != String.Empty)
                            {
                                while(!IPAddress.TryParse(result,out newip))
                                {
                                    Console.Write("Invalid IP address, Please enter again: ");
                                    result = Console.ReadLine();
                                }

                                SERVER_IP = result;
                            }
                            tryTimes = 4;
                        }
                    }

                } while (!connected);

                if (reqServReconn)
                {
            
                  SendRequest(Request.SERVRECONN + " " + playerName);
                
                }

                Console.WriteLine("Connected! Server IP: {0} Port: {1}", SERVER_IP, 8001);

            }
        }


        /// <summary>
        /// Communication between server and client method to send requests from clients to the server.
        /// </summary>
        /// <param name="msg"></param>
        public int SendRequest(string msg) 
        {
            
            string reqMessage = msg.Trim();

            string reqType, reqMsg;

            MessageParser.ParseNext(reqMessage, out reqType, out reqMsg);

            TcpClient thisTcpClient = client;

            switch (reqType)
            {
                case Request.GAME:
                    thisTcpClient = new TcpClient();
                    thisTcpClient.Connect(SERVER_IP, SERVER_PORT);
                    int inttest;
                    if (reqMsg == "" || !int.TryParse(reqMsg, out inttest))
                    {
                        Console.WriteLine("USAGE: game <number>");
                        thisTcpClient.Close();

                        return 0;
                    }

                    string numOfPeersToMatch = reqMsg;

                    reqMessage = reqType + " " + playerName + " " + numOfPeersToMatch;
                    break;

                case Request.RECONN:
                    int numTest;
                    if (reqMsg == "" || !int.TryParse(reqMsg, out numTest))
                    {
                        Console.WriteLine("USAGE: reconn <gameId>");
                        client.Close();
                        ConnectToServer();

                        return 0;
                    }
                    string gameId = reqMsg;

                    reqMessage = reqType + " " + playerName + " " + gameId;
                    break;

                case Request.CANCEL:
                    reqMessage = reqType + " " + playerName;
                    break;

                case Request.PLAYERS:                 
                case Request.CHECKNAME:
                case Request.SERVRECONN:
                    reqMessage = reqType + " " + reqMsg;
                    break;
            }            

            reqMessage += "\n\n";

            try {
                //Write to server
                TCPMessageHandler msgHandler = new TCPMessageHandler();
                string responseMessage = msgHandler.SendMessage(reqMessage, thisTcpClient);
                //Done writing to server

                ResponseStatus status = ProcessResponseMessage(responseMessage);

                switch (status)
                {
                    case ResponseStatus.Error:
                    case ResponseStatus.Ok:
                        thisTcpClient.Close();
                        // Connect back to server immediately if user not in game
                        if (reqType != Request.GAME)
                        {
                            ConnectToServer();
                        }
                        break;
                    case ResponseStatus.Game:
                        Console.WriteLine("!!!!!!!!!!!\nGame Matched!\n!!!!!!!!!!!\n");
                        Console.WriteLine("\tstarting...");
                        Thread.Sleep(800);

                        
                        thisTcpClient.Close();
                        // Interrupt any user input
                        var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                        PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                        break;
                }

                return (status==ResponseStatus.Error) ? -1 : 0;

            }
           
            catch (Exception)
            {
                thisTcpClient.Close();
                ConnectToServer(true);
                
            }

            return 0;
        }

        /// <summary>
        /// This method proccess messages coming from the server according to our design document
        /// </summary>
        /// <param name="responseMessage"></param>
        /// <returns></returns>
        private ResponseStatus ProcessResponseMessage(string responseMessage)
        {

            string respType;
            string respMsg;
            MessageParser.ParseNext(responseMessage, out respType, out respMsg);

            if (respType == Response.SUCCESS)
            {
                string reqType;

                MessageParser.ParseNext(respMsg, out reqType, out respMsg);

                Console.WriteLine("\nDEBUG: " + reqType + "\n");
                if (reqType == Request.GAME || reqType == Request.RECONN)
                {
                    allPeersInfo = new List<PeerInfo>();
  
                    string gameSessionId;
                    MessageParser.ParseNext(respMsg, out gameSessionId, out respMsg);
                    IEnumerable<string> temp = respMsg.Split(',');


                    allPeersInfo = temp.Where(elem => !string.IsNullOrEmpty(elem)).Select(info =>
                    {
                        string[] parsedInfo = info.Trim().Split(' ');
         
                        PeerInfo pInfo = null;
                        if (!string.IsNullOrEmpty(info))
                        {

                            string ip = parsedInfo[0];
                            int port = int.Parse(parsedInfo[1]);
                            string pName = parsedInfo[2];
                            int playerId = int.Parse(parsedInfo[3]);

                            pInfo = new PeerInfo(ip, port, pName, playerId, int.Parse(gameSessionId));
                        }

                        return pInfo;
                       
                    }).ToList();
                    
                    inGame = true;

                    switch (reqType)
                    {
                        case Request.GAME:
                            connectType = GameConnectType.NewGame; break;
                        case Request.RECONN:
                            connectType = GameConnectType.Reconnect; break;
                    }

                    return ResponseStatus.Game;
                }
                else if (reqType == Request.PLAYERS)
                {
                    // DISPLAY playernum ON GUI
                    string playernum = respMsg;
                    Console.WriteLine("\nNum of Players on server now: " + playernum);
                    return ResponseStatus.Ok;
                }
                else if (reqType == Request.CANCEL)
                {
                    // INDICATES THAT THE USER HAVE CANCELED 
                    Console.WriteLine("\nYou have CANCELED your match making.");
                    return ResponseStatus.Ok;
                }
                else if (reqType == Request.CHECKNAME)
                {
                    Console.WriteLine("\nName is available!");
                    return ResponseStatus.Ok;
                }else if (reqType == Request.SERVRECONN)
                {
                    if(respMsg != "") {
                        string reconnRespReqType;
                        MessageParser.ParseNext(respMsg, out reconnRespReqType, out respMsg);
                        if(reconnRespReqType == Request.GAME)
                        {
                            string playerNum = respMsg;
                            Console.WriteLine("You were in Queue for matchmaking for " + playerNum);

                            Task.Factory.StartNew(() => { SendRequest(Request.GAME + " " + playerNum); });
                        }
                    }
                    else
                    {
                        Console.WriteLine("Welcome back");
                    }
                    return ResponseStatus.Ok;
                }

            }
            else if (respType == Response.FAILURE)
            {

                string reqType;

                MessageParser.ParseNext(respMsg, out reqType, out respMsg);

                Console.WriteLine("\nDEBUG: " + reqType + "\n");
                Console.WriteLine("SERVER MESSAGE: " + respMsg);

                if (reqType == Request.GAME)
                {
                    
                    return ResponseStatus.Ok;
                }

                else if (reqType == Request.CHECKNAME)
                {

                    return ResponseStatus.Error;
                }else if(reqType == Request.RECONN)
                {

                    return ResponseStatus.Ok;
                }
                else if (reqType == Request.CANCEL)
                {

                    return ResponseStatus.Ok;
                }
            }
            else if (respType == Response.ERROR)
            {
                Console.WriteLine("\nSERVER MESSAGE: Err " + respMsg);
            }
            else
            {
                Console.WriteLine("SERVER MESSAGE: " + respType + " " + respMsg);
            }


            return ResponseStatus.Error;

        }

        public void StartClient()
        {

            // Continously stay connected to the server
    
                
            while (true)
            {
                try
                {

                    if (!inGame)
                    {
                         
                        Console.Write("Send request (game, players, cancel, reconn): ");
                        var request = Console.ReadLine().Trim().ToLower();



                        if (!inGame)
                        {
                                       
                           Console.WriteLine("Sending request \"{0}\"", request);

                           Task.Run(() => {SendRequest(request);  });

                        }
                       
                           
                    }

                    if(inGame)
                    {
                        // Leave the server if a game was matched, 
                        //   proceed to p2p connection with other players
                        if (client.Connected)
                        {
                            client.Close();
                        }

                        Peer peer;

                        using (peer = new Peer(playerName, allPeersInfo)) {
                            if (connectType == GameConnectType.Reconnect) {
                                peer.ReconnectBackToGame();
                            }
                            peer.StartPeerCommunication();
                        }

                        // Game ended, connect back to server
                        inGame = false;
                        ConnectToServer();
                    }
                }

                catch (Exception e)
                {
                    Console.WriteLine("--Error! Client Program Ended--");
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

                ClientProgram aClient = new ClientProgram();
                aClient.StartClient();


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
