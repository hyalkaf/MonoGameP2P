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
    
        const string RESP_SUCCESS = "success";
       

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

            String requestMessage = sb.ToString().Trim().ToLower();

            //Console.WriteLine("Recieved...");

            Console.WriteLine(requestMessage);
            string responseMessage = "I DO NOT UNDERSTAND THIS REQUEST";

            if (requestMessage.StartsWith(REQ_GAME))
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

                responseMessage = RESP_SUCCESS + " " + REQ_GAME + " You are matched with:\n" + players;

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
                //Remove this player from this queue
                playerQueue.Remove(thisClient);
            }
            else if (requestMessage.StartsWith(REQ_PLAYERS))
            {
                responseMessage = RESP_SUCCESS + " " + REQ_PLAYERS + "  " + sockets.Count;


            }
            else if (requestMessage.StartsWith(REQ_CANCEL))
            {
                // All the data has been read from the 
                // client. Display it on the console.
                responseMessage = RESP_SUCCESS + " " + REQ_CANCEL + " YOU CANCELED your match request.";

                // Echo the data back to the client.

            }

            ASCIIEncoding asen = new ASCIIEncoding();

            byte[] b = asen.GetBytes(responseMessage + "\n\n");

            Console.WriteLine("SIZE OF RESPONSE: " + b.Length);

            socket.Send(b);

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
