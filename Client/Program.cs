using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    public class ClientProgram
    {
        TcpClient client;

        public ClientProgram()
        {
            client = new TcpClient();
            Console.WriteLine("Connecting.....");

            client.Connect("127.0.0.1", 8001);
            // use the ipaddress as in the server program

            Console.WriteLine("Connected");
        }

        public void Send(string msg = "")
        {
            String str = msg;
            if (msg == "")
            {
                Console.Write("Request message was empty, please re-enter: ");

                str = Console.ReadLine();
            }

            Stream stm = client.GetStream();

            ASCIIEncoding asen = new ASCIIEncoding();
            byte[] ba = asen.GetBytes(str);
            Console.WriteLine("Transmitting your request to the server.....\n");

            stm.Write(ba, 0, ba.Length);

            byte[] bb = new byte[2048];
            int k = stm.Read(bb, 0, 2048);

            for (int i = 0; i < k; i++)
                Console.Write(Convert.ToChar(bb[i]));

        }

        static void Main(string[] args)
        {
            try
            {
                ClientProgram c1 = new ClientProgram();

                Console.Write("Send request (game, players, cancel): ");
                var request  = Console.ReadLine();

                new Thread(() => {
                    Console.WriteLine("Sending requestion \" {0} \"", request);
                    c1.Send(request);
                }).Start();

                Thread.Sleep(1500);

                Console.Write("--THE END--");
                Console.Read();
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
        }
    }
}
