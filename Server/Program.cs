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
        private ArrayList playerQueue;
        private ArrayList sockets;
        private TcpListener listener;
        private ManualResetEvent matchingMRE;
        private ManualResetEvent matchedMRE;
        private AutoResetEvent are = new AutoResetEvent(true);
        private int matchingCounter = 0;
        public ServerProgram()
        {
            sockets = new ArrayList();
            playerQueue = new ArrayList();
            matchingMRE = new ManualResetEvent(false);
            matchedMRE = new ManualResetEvent(false);
            /* Initializes the Listener */
            listener = new TcpListener(IPAddress.Any, 8001);
        }

        void EstablishConnection(object s, int id)
        {
            StringBuilder sb = new StringBuilder();
            Socket socket = (Socket)s;
            sockets.Add(socket);
            Console.WriteLine("Connection accepted from " + socket.RemoteEndPoint);

            byte[] buffer = new byte[2048];
            int bytesRead = socket.Receive(buffer);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            String request = sb.ToString().Trim().ToLower();

            Console.WriteLine("Recieved...");

            Console.WriteLine(request);
            string response = "I DO NOT UNDERSTAND THIS REQUEST";
            if (request.StartsWith("game"))
            {
                // All the data has been read from the 
                // client. Display it on the console.
 
                string thisClient = "[" + socket.LocalEndPoint.ToString() + " ID:" + id + "]";

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

                for(int i = 0; i < 2; i ++)
                {
                    players += i + " " + playerQueue[i] + ",";
                }

                response = "You are matched with:\n" + players;

                if (++matchingCounter < 2)
                {
                    matchedMRE.Reset();
                }
                else
                {
                    matchedMRE.Set();
                    matchingCounter = 0;
                }

                matchedMRE.WaitOne();
                foreach (var p in playerQueue)
                    Console.Write("{0}", p);

                Console.WriteLine("DEBUG: ");

                Thread.Sleep(799);
               // playerQueue.Remove(thisClient);
            }
            else if (request.StartsWith("players"))
            {
                // All the data has been read from the 
                // client. Display it on the console.
                response = "All players: " + sockets.ToString();

                // Echo the data back to the client.

            }
            else if (request == "cancel")
            {
                // All the data has been read from the 
                // client. Display it on the console.
                response = "YOU CANCELED your match request.";

                // Echo the data back to the client.

            }

            ASCIIEncoding asen = new ASCIIEncoding();
            socket.Send(asen.GetBytes("The string was recieved by the server.\n\n" + response + "\n\n"));

            Console.WriteLine("\nSent Acknowledgement");
            sockets.Remove(socket);

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
