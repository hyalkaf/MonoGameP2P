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
        TcpClient client;
        private string playerName;
        private int numberOfPeers;
        public bool inGame = false;

        public const string REQ_GAME = "game";
        public const string REQ_PLAYERS = "players";
        public const string REQ_CANCEL = "cancel";
        //public const string SERVER_IP = "127.0.0.1";
        public const string SERVER_IP = "10.211.55.4";
        const string RESP_SUCCESS = "success";

        // Holds information about other peers in the system: IPAddress, portNumber, name and ID.
        List<Tuple<string, int, string, int>> peersInfo;

        /// <summary>
        /// Constructor for creating a client, it will connect to the server and it will have a unique name.
        /// TODO: Change defualt name to something else
        /// </summary>
        /// <param name="player"></param>
        public ClientProgram(int numberOfPeers, string player = "NewPlayer")
        {
            connectToServer();
            playerName = player;
            this.numberOfPeers = numberOfPeers;

            // Generate two ports for sending and listening (8002 - 9999)
            
        }

        /// <summary>
        /// This method connects to the server for estiblishing a game.
        /// TODO: Should we have a specific IP and port or we should change it.
        /// </summary>
        private void connectToServer()
        {
            client = new TcpClient();
            Console.WriteLine("Connecting to Server.....");

            // Connect to the server
            client.Connect(SERVER_IP, 8001);
            
            // 
            Console.WriteLine("Connected to Server at IP {0} and port {1}", SERVER_IP, 8001);
            
        }


        /// <summary>
        /// Communication between server and client method to send requests from clients to the server.
        /// TODO: Why is msg have a defualt string? If client presses enter the empty string should be parsed as request too?
        /// </summary>
        /// <param name="msg"></param>
        public void SendRequest(string msg = "", int numberOfPeers = 2)
        {
            
            string reqMessage = msg;

            if(reqMessage == REQ_GAME || reqMessage == REQ_CANCEL)
            {
                reqMessage += " " + playerName + " " + numberOfPeers;
            }

            reqMessage += "\n\n";

            Stream stm = client.GetStream();

            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] ba = asen.GetBytes(reqMessage);

            Console.WriteLine("Transmitting your request to the server.....\n");
            stm.Write(ba, 0, ba.Length);

            byte[] bb = new byte[2048];
            Console.WriteLine("Waiting");
            int k = stm.Read(bb, 0, 2048);

            string responseMessage = "";
            char c = ' ';
            for (int i = 0; i < k; i++)
            {
                c = Convert.ToChar(bb[i]);
                responseMessage += c;
            }

           

            if (processResponse(responseMessage) == -1) {
                Console.WriteLine("\nDEBUG: INVALID REQUEST/RESPONSE\n");
            }

            client.Close();
            if (!inGame) {
                connectToServer();
            }
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
                    peersInfo = new List<Tuple<string, int, string, int>>();
                    IEnumerable<string> temp = responseMessage.Split(',');
                    peersInfo = temp.Where(elem => !string.IsNullOrEmpty(elem)).Select(info =>
                    {
                        string[] peerInfo = info.Trim().Split(' ');
                        Tuple<string, int, string, int> t = null;
                        if (!string.IsNullOrEmpty(info))
                        {
                            // TODO: deal with cases when integer can't be parsed
                            t = new Tuple<string, int, string, int>(peerInfo[0], int.Parse(peerInfo[1]), peerInfo[2], int.Parse(peerInfo[3])); 
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

            }

            return -1;

        }

        public void startClient()
        {

            while (true) { 
                try
                {

                    while (true)
                    {

                        if (!inGame)
                        {

                            Console.Write("Send request (game, players, cancel): ");
                            var request = Console.ReadLine().Trim().ToLower();

                            Console.WriteLine("Sending request \"{0}\"", request);
                            SendRequest(request, numberOfPeers);

                        }

                        else
                        {
                            break;

                        }
                    }

                    using (Peer peer = new Peer(playerName, peersInfo))
                    
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
                Console.Write("Enter Your Player Name: ");
                string pName = Console.ReadLine();

                Console.Write("Enter How many people you want to play with: ");
                // TODO: Add code to deal with cases when user enter something other than a int
                string numberOfPeers = Console.ReadLine();
                ClientProgram aClient = new ClientProgram(int.Parse(numberOfPeers), pName);
                aClient.startClient();


                Console.Write("--Program terminated. See you next time!--");
                Console.Read();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
        }
    }
}
