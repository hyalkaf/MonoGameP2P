using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    /// <summary>
    /// This class will be used to send and receive udp messages.
    /// </summary>
    class BroadcastForReplication
    {
        // TODO: Write a method to calculate IP address of broadcast so you don't
        // have to do it every time.
        // IP Endpoints that will be used to send broadcast messages and another one for receiving.
        IPEndPoint sendingIP = new IPEndPoint(IPAddress.Parse("10.1.15.255"), 15000);
        IPEndPoint receivingIP = new IPEndPoint(IPAddress.Any, 0);

        // Udp client listening for broadcast messages that are sent by every new server 
        // through its replication manager.
        public UdpClient receiveBroadcastUDPClient { get; set; }
        private int portNumber { get; set; }

        // This timer is used for timing out broadcast messages for finding primary server
        // in the local network. Once time has passed after broadcasting without receiving
        // responses then it will run its callback making the server primary. 
        private static readonly int FINDING_PRIMARY_COUNTDOWN = 2000;
        private Timer timerForFindingPrimary;

        // NUMBER_OF_TIMER_TO_SEND_UDP is the number of times or tries
        // to send udp messages in case they were messing or weren't received.
        private static readonly int NUMBER_OF_TRIES_TO_SEND_UDP = 3;

        // isUdpResponseReceived will be used to figure out whether broadcase messages 
        // where received by others from their responses.
        bool isUdpResponseReceived = false;

        // Request and response messsages between servers in the local network
        // These messages are broadcasted to all other servers listening on the specified
        // port number. These are mentioned in the design document.
        // REQ_IS_PRIMARY_THERE is a request message that will be sent whenever a new backup is initialized
        // It will be broadcasted in an attempt to find the primary server in the local network.
        private static readonly string REQ_IS_PRIMARY_THERE = "isPrimary";
        // RES_PRIMARY_FOUND is a response message that will be sent on the request of other servers trying
        // to find primary server. This will only be sent by the primary server.
        private static readonly string RES_PRIMARY_FOUND = "primary";

        // primaryFound will be used to not run the same code multiple times in case 
        // Primary was detected. TODO: There is a better way for doing this.
        bool primaryFound = false;

        // The assoicated replication manager that uses this udp Wrapper
        ReplicationManager associatedReplicationManager;

        /// <summary>
        /// Constrcutor for initializing a new udp Client. 
        /// </summary>
        /// <param name="isBroadCast">Determine whether this udp client will be broadcasting</param>
        public BroadcastForReplication (bool isBroadCast, int portNumber, ReplicationManager associatedReplicationManager)
        {
            // Initialize udpClient
            receiveBroadcastUDPClient = new UdpClient(portNumber);
            this.portNumber = portNumber;
            receiveBroadcastUDPClient.EnableBroadcast = isBroadCast;
            this.associatedReplicationManager = associatedReplicationManager;

            Thread udpListenThread = new Thread(() =>
            {
               StartListening();
            });

            udpListenThread.Start();

            // Initalize timer countdown before becoming primary server
            timerForFindingPrimary = new Timer(timerCallBackForFindingPrimary, REQ_IS_PRIMARY_THERE, FINDING_PRIMARY_COUNTDOWN, Timeout.Infinite);

            // While primary hasn't been found and there wasn't any reply from other server 
            // continue sending messages number of times. 
            for (int i = 0; i < NUMBER_OF_TRIES_TO_SEND_UDP; i++)
            {
                if (!isUdpResponseReceived)
                {
                    SendMessage(REQ_IS_PRIMARY_THERE);
                }
                Thread.Sleep(500);
            }

        }


        // <summary>
        // this method will start listening for incoming requests to check if backup is primary or not
        // </summary>
        private void StartListening()
        {
            while (true)
            {
                //receive messages
                byte[] bytes = receiveBroadcastUDPClient.Receive(ref receivingIP);
                string message = Encoding.ASCII.GetString(bytes);
                Console.WriteLine("I received {0}", message);
                // todo: disable sending messages to yourself by default
                if (!receivingIP.Address.Equals(associatedReplicationManager.thisServer.ipAddr)) ParseBroadcastMessages(message, receivingIP);
            }
        }

        /// <summary>
        /// This method will broadcast a message to all ip addresses in the local newtork.
        /// </summary>
        /// <param name="message">Message to be broadcasted to all local network peers.</param>
        private void SendMessage(string message)
        {
            // Initialize a new udp client
            IPEndPoint ipEndPoint = new IPEndPoint(associatedReplicationManager.thisServer.ipAddr, portNumber);
            using (UdpClient client = new UdpClient(ipEndPoint))
            {
                client.EnableBroadcast = true;

                // Send a request message asking if primary exists.
                byte[] bytes = Encoding.ASCII.GetBytes(message);

                // Send message
                client.Send(bytes, bytes.Length, sendingIP);

                Console.WriteLine("I sent {0}", message);
            }
        }

        /// <summary>
        /// This method will parse incoming requests that are sent using broadcase udp.
        /// </summary>
        /// <param name="receivedMessage">Message to be parsed</param>
        private void ParseBroadcastMessages(string receivedMessage, IPEndPoint ip)
        {
            // Parse message received 
            if (receivedMessage.StartsWith(REQ_IS_PRIMARY_THERE))
            {
                isUdpResponseReceived = true;
                // Check if this backup server is primary
                if (associatedReplicationManager.thisServer.isPrimaryServer)
                {
                    // Send a response back
                    // TODO: Only send to specific ip.
                    // Don't broadcast 
                    SendMessage(RES_PRIMARY_FOUND);
                    // Test: send to specific ip
                    // Initialize a new udp client
                    //UdpClient client = new UdpClient(AddressFamily.InterNetwork);

                    //// Send a request message asking if primary exists.
                    //byte[] bytes = Encoding.ASCII.GetBytes("primary");

                    //// Send message
                    //ip.Port = 15000;
                    //client.Send(bytes, bytes.Length, ip);

                    //Console.WriteLine("I sent {0}", "primary");

                    //// Close client
                    //client.Close();


                }
            }
            else if (receivedMessage.StartsWith(RES_PRIMARY_FOUND) && !primaryFound)
            {
                isUdpResponseReceived = true;
                primaryFound = true;

                // Disable timer 
                timerForFindingPrimary.Change(Timeout.Infinite, Timeout.Infinite);

                // Make this server a backup
                associatedReplicationManager.thisServer.isPrimaryServer = false;

                // DEBUG
                Console.WriteLine("Primary was found, this server is backup");

                associatedReplicationManager.InitializeReplicationManager(false, ip.Address);
            }
        }

        /// <summary>
        /// This method is a callback for a timer where it's being called when a server doesn't get any reply when it's initialized.
        /// The server becomes the primary server when that happens.
        /// </summary>
        /// <param name="state">Passed parameter to the call back -> Object</param>
        private void timerCallBackForFindingPrimary(object state)
        {
            associatedReplicationManager.thisServer.isPrimaryServer = true;

            Console.WriteLine("I'm primary");
            associatedReplicationManager.addReplica(associatedReplicationManager.thisServer);

            associatedReplicationManager.thisServer.StartListen();

        }
    }
}
