using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharedCode
{
    public class TCPMessageHandler
    {


        public string SendMessage(string msg, TcpClient tcpclient)
        {

            try
            {
                NetworkStream netStream = tcpclient.GetStream();

                byte[] bytesToSend = Encoding.ASCII.GetBytes(msg);
                netStream.Write(bytesToSend, 0, bytesToSend.Length);

                tcpclient.ReceiveBufferSize = 4096;
                byte[] bytesRead = new byte[tcpclient.ReceiveBufferSize];

                netStream.Read(bytesRead, 0, (int)tcpclient.ReceiveBufferSize);
                // Console.WriteLine("... OK!");

                string responseMessage = Encoding.ASCII.GetString(bytesRead);
                return responseMessage.Substring(0, responseMessage.IndexOf("\0")).Trim();
            }
            catch (Exception e)
            {

                throw e;
            }

        }
        public string RecieveMessage(TcpClient tcpclient)
        {
            try
            {
                NetworkStream netStream = tcpclient.GetStream();

                tcpclient.ReceiveBufferSize = 4096;
                byte[] bytes = new byte[tcpclient.ReceiveBufferSize];

                netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);

                string requestMessage = Encoding.ASCII.GetString(bytes).Trim();
                return requestMessage.Substring(0, requestMessage.IndexOf("\0")).Trim();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public void SendResponse(string msg, TcpClient tcpclient)
        {
            try { 

                byte[] byteToSend = Encoding.ASCII.GetBytes(msg);
                tcpclient.GetStream().Write(byteToSend, 0, byteToSend.Length);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
