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
        
    class ReplicationManager
    {
        // Primary server ip address
        public static IPAddress primaryServerIp = IPAddress.Parse("0.0.0.0");
        // CH: This way of storing all replicas might not be viable
        public static List<ServerProgram> listReplicas = new List<ServerProgram>();
        // CH: New way of storing replicas (IPAddress, Bool: online status)
        public static List<Tuple<IPAddress,bool>> allReplicaAddr = new List<Tuple<IPAddress,bool>>();
        // 
        private TcpClient replicaClient;
        //
        Timer timer;

        // Request messsage between replicas and server
        const string REQ_REPLICA = "replica";
        const string REQ_INFO = "info";
        const string REQ_CHECK = "check";
        const string RESP_SUCCESS = "success";


        private ServerProgram thisServer;
        public ReplicationManager(ServerProgram replica, IPAddress primaryServerIPAddress)
        {
            
            // rmListener = new TcpListener();
            primaryServerIp = primaryServerIPAddress;
            if (!replica.isPrimaryServer)
            {
                // Communicate with the primary server to get info about the game
                timer = new Timer(CheckServerExistence, "Some state", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
            else
            {
                addReplica(replica);
            }

            thisServer = replica;
            
            // Run listening on its own thread
            new Thread(() =>
            {
                ListenReplica();
            }).Start();

            // secondary replica sends a replica request
            if (!replica.isPrimaryServer)
            {
                SendReplica("replica");
            }


        }

        public void EstablishConnection(Socket sock)
        {
            Console.WriteLine("Establishing Connection with {0} {1}", (sock.RemoteEndPoint as IPEndPoint).Address, (sock.LocalEndPoint as IPEndPoint).Address);
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
            string responseMessage;
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
                    responseMessage = REQ_INFO + " ";
                    for (int i = 0; i < allReplicaAddr.Count; i++)
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
            
            else if (requestType == REQ_INFO)
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
                        if (allReplicaAddr.All(tuple => tuple.Item1 != ipAddr))
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
                    responseMessage = RESP_SUCCESS + " " + REQ_INFO + "  ";

                    ASCIIEncoding asen = new ASCIIEncoding();

                    b = asen.GetBytes(responseMessage + "\n\n");
                }

                foreach (Tuple<IPAddress, bool> replica in allReplicaAddr)
                {
                    Console.WriteLine("The replicas are: {0} {1}", replica.Item1, replica.Item2);
                }
            }
            /*else if (requestType == REQ_CHECK)
            {
                Console.WriteLine("I'm Checking the primary server");
            }*/
            else if (requestType == RESP_SUCCESS)
            {
                string success_message = messageParam.Substring(0, messageParam.IndexOf(" ")).Trim();

                if (success_message.StartsWith(REQ_REPLICA))
                {
                    allReplicaAddr.Add(new Tuple<IPAddress, bool>(thisServer.ipAddr, true));
                }
            }

            return b;
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
        public void SendReplica(string tempMsg)
        {
            string messageToBeSent = string.Empty;

            // Check tryp of message
            if (tempMsg.StartsWith(REQ_INFO))
            {
                // add required information to be sent back
                messageToBeSent = "info" + " ";
                for (int i = 0; i < allReplicaAddr.Count; i++)
                {
                    // Comma shouldn't be added at the end of the message
                    if (i != allReplicaAddr.Count - 1)
                    {
                        messageToBeSent += allReplicaAddr[i].Item1 + ",";
                    }
                    else
                    {
                        messageToBeSent += allReplicaAddr[i].Item1;
                    }
                }
            }
            else if (tempMsg.StartsWith(REQ_REPLICA))
            {
                // Message to be sent 
                messageToBeSent = "replica" + " " + thisServer.ipAddr;
            }
            else if (tempMsg.StartsWith(REQ_CHECK))
            {
                // Message to be sent 
                messageToBeSent = "check" + " ";
            }

            // Initalize a new TcpClient
            replicaClient = new TcpClient();

            // Catch errors in case we are checking server existence
            try
            {

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
            catch(Exception e)
            {
                // Check what type of message was being sent
                if (tempMsg.StartsWith(REQ_CHECK))
                {
                    // In this case: server must have crashed
                    // take over and become the primary 
                    // TODO: This won't work for multiple servers
                    if (allReplicaAddr[1].Item1 == thisServer.ipAddr)
                    {
                        MakeThisServerPrimary();
                    }
                    
                }
            }
  
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
        }

        public bool IsPrimary(IPAddress ipAddr)
        {
            return ipAddr == primaryServerIp;
        }

        /// <summary>
        /// This method checks that the primary server exists at all time.
        /// </summary>
        /// <param name="state">This parameter has to be passed even though we don't need it here.</param>
        private void CheckServerExistence(object state)
        {
            // Send to primary a message
            SendReplica("check");
        }
        
    }
}
