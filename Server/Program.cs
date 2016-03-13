using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;

namespace Server
{
    public class ServerProgram
    {
        const string REQ_GAME = "game";
        const string REQ_PLAYERS = "players";
        const string REQ_CANCEL = "cancel";
        const string REQ_IP = "ip";
        const string RESP_SUCCESS = "success";
        
        private List<string> playerQueue;
        private Object thisLock = new Object();
        private int portNumber = 9000;
        private List<Socket> sockets;
        
        // Will use index as number of clients who want to be matched with this amount of other clients
        // then once that index has fullfilled its number we will match those in that index to a game.
        private List<Dictionary<string, Socket>> socketsForGameRequests;

        private Dictionary<int, IPAddress> portToIPAddresses;
        private TcpListener listener;
        private ManualResetEvent matchingMRE;
        private ManualResetEvent matchedMRE;
        private AutoResetEvent are = new AutoResetEvent(true);
        private int matchingCounter = 0;

        public ServerProgram()
        {
            sockets = new List<Socket>();
            socketsForGameRequests = new List<Dictionary<string, Socket>>();
            playerQueue = new List<string>();
            matchingMRE = new ManualResetEvent(false);
            matchedMRE = new ManualResetEvent(false);

            /* Initializes the Listener */
            listener = new TcpListener(IPAddress.Any, 8001);
        }

        void EstablishConnection(Socket s, int id)
        {
            StringBuilder sb = new StringBuilder();
            sockets.Add(s);



            Console.WriteLine("Connection accepted from "); //+ ipaddr + " : " + portNumber);

            byte[] buffer = new byte[2048];
            int bytesRead = s.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            string requestMessage = sb.ToString().Trim().ToLower();
            string requestType = "";
            if (requestMessage.IndexOf(" ") == -1)
            {
                requestType = requestMessage;
            }
            else
            {
                requestType = requestMessage.Substring(0, requestMessage.IndexOf(" ")).Trim();
            }
            
            requestMessage = requestMessage.Substring(requestType.Length).Trim();
            //Console.WriteLine("Recieved...");

            Console.WriteLine(requestMessage);
            string responseMessage = "error I DO NOT UNDERSTAND THIS REQUEST";

            if (requestType == REQ_GAME)
            {
                // All the data has been read from the 
                // client. Display it on the console.
                string pName = requestMessage.Substring(0, requestMessage.IndexOf(" "));

                // TODO: Deal with cases where parsing doesn't work
                int numberOfPeers = int.Parse(requestMessage.Substring(pName.Length));

                // Add this socket to the match making list of list of sockets
                if (numberOfPeers >= socketsForGameRequests.Count)
                {
                    for (int i = 0; i <= numberOfPeers; i++)
                    {
                        socketsForGameRequests.Add(new Dictionary<string, Socket>());
                    }                    
                }

                socketsForGameRequests[numberOfPeers][pName] = s;
                // Run a thread for checking if a match should occur
                // A match occurs when an index in the list has number of peers(players) equal to its index.
                new Thread(() => {
                    lock (thisLock)
                    {
                        for (int i = 0; i < socketsForGameRequests.Count; i++)
                        {
                            // bypass first and second index since there are no matches with 0 or 1 player
                            if (i != 0 && i != 1)
                            {
                                if (i == socketsForGameRequests[i].Count)
                                {
                                    // Do match between peers
                                    foreach (KeyValuePair<string, Socket> dicNameToSocket in socketsForGameRequests[i]) 
                                    {
                                        // CH : Get endpoint 
                                        IPEndPoint remoteIpEndPoint = dicNameToSocket.Value.RemoteEndPoint as IPEndPoint;

                                        // CH : Get ip address from the endpoint
                                        IPAddress ipaddr = remoteIpEndPoint.Address;

                                        // Assign the ip address to a port
                                        //TODO: remove this? // portToIPAddresses.Add(portNumber, ipaddr);
                                        string thisClient = ipaddr + " " + portNumber++ + " " + dicNameToSocket.Key;

                                        playerQueue.Add(thisClient);
                                    }

                                    string playersToBeSent = "";

                                    for (int z = 0; z < playerQueue.Count; z++)
                                    {
                                        playersToBeSent += playerQueue[z] + " " + z;
                                        if (z != playerQueue.Count - 1)
                                        {
                                            playersToBeSent += ",";
                                        }
                                    }

                                    responseMessage = RESP_SUCCESS + " " + REQ_GAME + " " + playersToBeSent;

                                    foreach (KeyValuePair<string, Socket> dicNameToSocket in socketsForGameRequests[i])
                                    {
                                        Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                                        ASCIIEncoding asen = new ASCIIEncoding();

                                        byte[] b = asen.GetBytes(responseMessage + "\n\n");

                                        Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

                                        dicNameToSocket.Value.Send(b);

                                        Console.WriteLine("\nSent Acknowledgement");
                                    }

                                    playerQueue.Clear();
                                }

                            }
                        }
                    }
                }).Start();

                // Since we added 

                /*//IF PNAME NOT IN QUEUE, THEN PNAME+"2"
                string thisClient = s.RemoteEndPoint.ToString() + " " + pName;

                playerQueue.Add(thisClient);

                if (playerQueue.Count < 2)
                {
                    matchingMRE.Reset();

                }
                else
                {

                    matchingMRE.Set();
                }
                // Hangs until at least two players are in queue
                matchingMRE.WaitOne();


                string players = "";

                for (int i = 1; i <= 2; i++)
                {
                    players += playerQueue[i - 1] + " " + i + ",";
                }

                //pNameplayers.Substring(0,players.Length-1); ELEMINATE LAST COMMA?
                responseMessage = RESP_SUCCESS + " " + REQ_GAME + " " + players;

                if (++matchingCounter < 2)
                {
                    matchedMRE.Reset();
                }
                else
                {
                    matchedMRE.Set();
                    matchingCounter = 0;
                }

                //Hangs until response messages are formed complete for every matched player
                matchedMRE.WaitOne();

                Thread.Sleep(1000);

                // Remove this player from this queue
                if(playerQueue.Exists(player => (player == thisClient)))
                {
                    Console.WriteLine("Yo this exists");
                    playerQueue.Remove(thisClient);
                }*/


            }
            else if (requestType == REQ_PLAYERS)
            {
                responseMessage = RESP_SUCCESS + " " + REQ_PLAYERS + "  " + sockets.Count;
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                ASCIIEncoding asen = new ASCIIEncoding();

                byte[] b = asen.GetBytes(responseMessage + "\n\n");

                Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

                s.Send(b);

                Console.WriteLine("\nSent Acknowledgement");
                if (sockets.Exists(soc => soc == s))
                {
                    sockets.Remove(s);
                }

            }
            else if (requestType == REQ_CANCEL)
            {
                // All the data has been read from the 
                // client. Display it on the console.
                responseMessage = RESP_SUCCESS + " " + REQ_CANCEL + " YOU CANCELED your match request.";

                // Echo the data back to the client.
                Console.WriteLine("DEBUG: Response sent: " + responseMessage);

                ASCIIEncoding asen = new ASCIIEncoding();

                byte[] b = asen.GetBytes(responseMessage + "\n\n");

                Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

                s.Send(b);

                Console.WriteLine("\nSent Acknowledgement");
                if (sockets.Exists(soc => soc == s))
                {
                    sockets.Remove(s);
                }

            }


        }

        public void StartListen()
        {
            /* Start Listeneting at the specified port */
            listener.Start();

            Console.WriteLine("The server is running at port 8001...");
            Console.WriteLine("The local End point is  :" +
                              listener.LocalEndpoint);
            int counter = 0;
            do
            {
                counter++;
                try
                {

                
                    Console.WriteLine("Waiting for a connection {0} .....", counter);
                    Socket s = listener.AcceptSocket();

                    new Thread(() => {
                        EstablishConnection(s, counter);
                    }).Start();
                

                
                }
                catch (Exception e)
                {
                    Console.WriteLine("Something went wrong!");
                    listener.Stop();
                    Console.WriteLine(e.StackTrace);
                }
            } while (true);
            /* clean up */

          
        }

        static void Main(string[] args)
        {
            try
            {
                ServerProgram svr = new ServerProgram();
                svr.StartListen();

            }
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
        }
    }

}
