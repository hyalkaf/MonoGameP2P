﻿using System;
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


        TcpClient client;
        TcpClient stayConnectedClient;
        private string playerName;
        public bool inGame = false;
        private bool gameRequested = false;
        public static string SERVER_IP = "";
        private GameConnectType connectType;
        // Holds information about other peers in the system: IPAddress, portNumber, name and ID.
        //List<Tuple<string, int, string, int, int>> peersInfo;
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

            // Connect to server and set unique player name 
            string pName = String.Empty;
            int checkNameResult = -1;
 
            do
            {
                connectToServer();
                Console.Write("Enter Your Player Name: ");
                pName = Console.ReadLine();
                pName = pName.Replace(" ", "").Replace("\t", "");

                checkNameResult = SendRequest(Request.CHECKNAME + " " + pName);
                if (checkNameResult == -1)
                {
                    Console.Write("Name exists on server, is it you? (Y/N)");
                    string isityou = Console.ReadLine().Trim().ToLower();

                    if(isityou == "y")
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
        private void connectToServer()
        {
            //if ((client is TcpClient) && client.Connected)
            //{
            //    return;
            //}

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
                }catch (Exception)
                {
                    connected = false;
                    client.Close();
                }

            } while (!connected);

            
            Console.WriteLine("Connected! Server IP: {0} Port: {1}", SERVER_IP, 8001);


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

            if (reqType == Request.GAME)
            {
         
                int inttest;
                if (reqMsg == "" || !int.TryParse(reqMsg, out inttest))
                {
                    Console.WriteLine("USAGE: game <number>");
                    thisTcpClient.Close();
                    if (!inGame)
                    {
                        // Connect back to server immediately if user not in game
                        connectToServer();
                    }
                    return 0 ;
                }

                string numOfPeersToMatch = reqMsg;

                reqMessage = reqType +  " " + playerName + " " + numOfPeersToMatch;

            }
            else if(reqType == Request.CANCEL)
            {
                reqMessage = reqType;
            }
            else if (reqType == Request.PLAYERS)
            {
                reqMessage = reqType;
            }
            else if (reqType == Request.CHECKNAME)
            {
                reqMessage = reqType + " " + reqMsg;
            }
            else if (reqType == Request.RECONN)
            {
                int inttest;
                if (reqMsg == "" || !int.TryParse(reqMsg, out inttest))
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
                string gameId = reqMsg;

                reqMessage = reqType + " " + playerName + " " + gameId;
            }
            else if (reqType == Request.SERVRECONN)
            {
                reqMessage = reqType + " " + reqMsg;
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
                
                NetworkStream clientNetStream = thisTcpClient.GetStream();

                //Write to server
                ASCIIEncoding asen = new ASCIIEncoding();
                byte[] ba = asen.GetBytes(reqMessage);
                Console.Write("Transmitting\n\t" + reqMessage + "\nto the server.....");
                clientNetStream.Write(ba, 0, ba.Length);
                //Done Write

                //Read response from server (Hangs here until server responds)
                byte[] bytesRead = new byte[thisTcpClient.ReceiveBufferSize];
                clientNetStream.Read(bytesRead, 0, (int)thisTcpClient.ReceiveBufferSize);
                Console.WriteLine(" OK!");
                //Done Read

                string responseMessage = Encoding.ASCII.GetString(bytesRead).Trim();
                responseMessage = responseMessage.Substring(0, responseMessage.IndexOf("\0")).Trim();

                if (processResponse(responseMessage) == -1)
                {
                    //Console.WriteLine("\nDEBUG: INVALID REQUEST/RESPONSE\n");
                    thisTcpClient.Close();
                    connectToServer();
                    return -1;
                }

                thisTcpClient.Close();
                if (!inGame)
                {
                    // Connect back to server immediately if user not in game
                    connectToServer();
                }
            }
            catch (Exception)
            {
                thisTcpClient.Close();
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
            
            string respType;
            string respMsg;
            MessageParser.ParseNext(responseMessage, out respType, out respMsg);

            if (respType == Response.SUCCESS)
            {
               // responseMessage = responseMessage.Substring(Response.SUCCESS.Length).Trim();

                string reqType;

                MessageParser.ParseNext(respMsg, out reqType, out respMsg);

                //string requestType = responseMessage.Substring(0, responseMessage.IndexOf(" ")).Trim();
               // responseMessage = responseMessage.Substring(requestType.Length);

                Console.WriteLine("\nDEBUG: " + reqType + "\n");
                if (reqType == Request.GAME)
                {
                    allPeersInfo = new List<PeerInfo>();
                    //peersInfo = new List<Tuple<string, int, string, int, int>>();
                    IEnumerable<string> temp = respMsg.Split(',');
                    allPeersInfo = temp.Where(elem => !string.IsNullOrEmpty(elem)).Select(info =>
                    {
                        string[] parsedInfo = info.Trim().Split(' ');
                        //Tuple<string, int, string, int, int> t = null;
                        PeerInfo pInfo = null;
                        if (!string.IsNullOrEmpty(info))
                        {
                            // TODO: deal with cases when integer can't be parsed
                            string ip = parsedInfo[0];
                            int port = int.Parse(parsedInfo[1]);
                            string pName = parsedInfo[2];
                            int playerId = int.Parse(parsedInfo[3]);
                            int gameSessionId = int.Parse(parsedInfo[4]);
                            // TODO: deal with cases when integer can't be parsed
                            pInfo = new PeerInfo(ip, port, pName, playerId, gameSessionId);
                        }

                        //return t;
                        return pInfo;
                       
                    }).ToList();

                    inGame = true;
                    connectType = GameConnectType.NewGame;
                    return 0;
                }
                else if (reqType == Request.RECONN)
                {
                    allPeersInfo = new List<PeerInfo>();
                
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
                            int gameSessionId = int.Parse(parsedInfo[4]);

                            // TODO: deal with cases when integer can't be parsed
                            pInfo = new PeerInfo(ip, port, pName, playerId, gameSessionId);
                        }

      
                        return pInfo;

                    }).ToList();

                    inGame = true;
                    connectType = GameConnectType.Reconnect;
                    return 0;
                }
                else if (reqType == Request.PLAYERS)
                {
                    // DISPLAY playernum ON GUI
                    string playernum = respMsg;
                    Console.WriteLine("\nNum of Players on server now: " + playernum);
                    return 0;
                }
                else if (reqType == Request.CANCEL)
                {
                    // INDICATES THAT THE USER HAVE CANCELED 
                    Console.WriteLine("\nYou have CANCELED your match making.");
                    return 0;
                }
                else if (reqType == Request.CHECKNAME)
                {
                    Console.WriteLine("\nName is available!");
                    return 0;
                }else if (reqType == Request.SERVRECONN)
                {
                    if(respMsg != "") {
                        string reconnRespReqType;
                        MessageParser.ParseNext(respMsg, out reconnRespReqType, out respMsg);
                        if(reconnRespReqType == Request.GAME)
                        {
                            string playerNum = respMsg;
                            Console.WriteLine("You were in Queue for matchmaking for " + playerNum);
                            
                        }
                    }
                    else
                    {
                        Console.WriteLine("Welcome back");
                    }
                    return 0;
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
                    
                    return 0;
                }

                else if (reqType == Request.CHECKNAME)
                {

                    return -1;
                }else if(reqType == Request.RECONN)
                {

                    return 0;
                }
                else if (reqType == Request.CANCEL)
                {

                    return 0;
                }
            }
            else if (respType == Response.ERROR)
            {
                Console.WriteLine("SERVER MESSAGE: Err " + respMsg);
            }
            else
            {
                Console.WriteLine("SERVER MESSAGE: " + respType + " " + respMsg);
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
                            if (request != String.Empty) {

                                Console.WriteLine("Sending request \"{0}\"", request);
                                SendRequest(request);
                                //Thread sendThread = new Thread( () =>{

                                //    Thread.CurrentThread.Abort();
                                //});

                                //sendThread.IsBackground = true;
                                //sendThread.Start();
                            }

                        }

                        else
                        {
                            // Leave the server if a game was matched, 
                            //   proceed to p2p connection with other players
                            break;
                        }
                    }

                    bool reconn = false;
                    if (connectType == GameConnectType.Reconnect)
                    {
                        reconn = true;
                    }

                    Peer peer;

                    using (peer = new Peer(playerName, allPeersInfo, reconn)) 
                    peer.StartPeerCommunication();
                    
                    // Game ended connect back to server
                    inGame = false;
                    connectToServer();

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
