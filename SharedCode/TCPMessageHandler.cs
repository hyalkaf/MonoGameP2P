using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharedCode
{
    /// <summary>
    /// This class is used to send, receive and send reponses using TCPClient
    /// Basically a wrapper around TCPClient.
    /// </summary>
    public class TCPMessageHandler
    {
        // Global variables for this class
        private static readonly int BUFFER_SIZE = 4096;

        /// <summary>
        /// This method sends a message using the tcp client passed to it.
        /// It will bubble the an exception if one was thrown.
        /// </summary>
        /// <param name="msg">Message to be sent</param>
        /// <param name="tcpclient">Client to be used to sedning</param>
        /// <returns>response message</returns>
        public string SendMessage(string msg, TcpClient tcpclient)
        {

            try
            {
                // Get stream for message
                NetworkStream netStream = tcpclient.GetStream();

                // convert to bytes to write to stream
                byte[] bytesToSend = Encoding.ASCII.GetBytes(msg);
                netStream.Write(bytesToSend, 0, bytesToSend.Length);

                // receive response in bytes
                tcpclient.ReceiveBufferSize = BUFFER_SIZE;
                byte[] bytesRead = new byte[tcpclient.ReceiveBufferSize];

                // read it to just initialized bytes
                netStream.Read(bytesRead, 0, (int)tcpclient.ReceiveBufferSize);
                // Console.WriteLine("... OK!");

                // convert message to string and trim white spaces 
                string responseMessage = Encoding.ASCII.GetString(bytesRead);
                return responseMessage.Substring(0, responseMessage.IndexOf("\0")).Trim();
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// This method receives a message from tcp client and returns it
        /// </summary>
        /// <param name="tcpclient">TCPclient to be used for receiving message</param>
        /// <returns>message received</returns>
        public string RecieveMessage(TcpClient tcpclient)
        {
            try
            {
                /// Get stream for message 
                NetworkStream netStream = tcpclient.GetStream();

                // receive message in bytes
                tcpclient.ReceiveBufferSize = BUFFER_SIZE;
                byte[] bytes = new byte[tcpclient.ReceiveBufferSize];

                // read it to just initialized bytes
                netStream.Read(bytes, 0, (int)tcpclient.ReceiveBufferSize);

                // convert message to string and trim white spaces 
                string requestMessage = Encoding.ASCII.GetString(bytes).Trim();
                return requestMessage.Substring(0, requestMessage.IndexOf("\0")).Trim();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// This method send a response back to the specified tcp client.
        /// </summary>
        /// <param name="msg">response message</param>
        /// <param name="tcpclient">tcp client connected waiting for a response</param>
        public void SendResponse(string msg, TcpClient tcpclient)
        {
            try
            { 
                // Write to tcp client the bytes of message after converting it
                byte[] byteToSend = Encoding.ASCII.GetBytes(msg);
                tcpclient.GetStream().Write(byteToSend, 0, byteToSend.Length);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// This method will be used to send messages but it will create its own tcp client
        /// </summary>
        /// <param name="ip">IPAddress of tcp client</param>
        /// <param name="portNumber">Port number for tcp client</param>
        /// <param name="message">message to be sent</param>
        /// <returns>Response message after sending a message</returns>
        public string SendMessage(IPAddress ip, int portNumber, string message)
        {
            string response = string.Empty;

            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    // Initialize tcp client and send message to it
                    tcpClient.Connect(ip, portNumber);
                    TCPMessageHandler tcpMessagehandler = new TCPMessageHandler();
                    response = tcpMessagehandler.SendMessage(message, tcpClient);
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return response;
        }
    }
}
