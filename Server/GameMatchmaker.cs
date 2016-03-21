using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class GameMatchmaker
    {
        
        public static int idCounter = 1;
        private int portNumber = 9000;
        private List<GameSession> gameSessions;
        private List<ConcurrentQueue<ClientInfo>> clientsWaitingForGame;

        public GameMatchmaker() {
            gameSessions = new List<GameSession>();
            clientsWaitingForGame = new List<ConcurrentQueue<ClientInfo>>();
        }

        public void NewGameSession(GameSession gs)
        {
            gameSessions.Add(gs);
        }

        public void RemoveGameSession(int id)
        {
            gameSessions.Remove(gameSessions.Where(gs => gs.ID == id).First());
        }

        public GameSession GetGameSession(int id)
        {
            return gameSessions.Where(gs => gs.ID == id).First();
        }

        public int IsInQueue(string playername, ClientInfo clientInfo)
        {
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                ConcurrentQueue<ClientInfo> q = clientsWaitingForGame[i];
                foreach (ClientInfo ci in q)
                {
                    if (ci.PlayerName == playername)
                    {
                        ci.TcpClient = clientInfo.TcpClient;
                        ci.IPAddr = (clientInfo.TcpClient.Client.RemoteEndPoint as IPEndPoint).Address;
                        
                        return i;
                    }
                }
               
            }
            return -1;

        }

        public void AddPlayerToQueue(ClientInfo player, int queueNum)
        {
            if (queueNum >= clientsWaitingForGame.Count)
            {
                for (int i = clientsWaitingForGame.Count; i <= queueNum; i++)
                {
                    clientsWaitingForGame.Add(new ConcurrentQueue<ClientInfo>());
                }
            }

            clientsWaitingForGame[queueNum].Enqueue(player);

        }

        public void MatchPeers(ServerProgram server)
        {
            string responseMessage = string.Empty;
            //string playersToBeSent = "";
            //List<string> playersToBeSentList = new List<string>();
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                // bypass first and second index since there are no matches with 0 or 1 player
                if (i != 0 && i != 1)
                {
                    // TODO: Will not work when in index 2 there are four want 2
                    if (i <= clientsWaitingForGame[i].Count)
                    {

                        // First, test if the 'connected' queued players are still online
                        int stillConnected = clientsWaitingForGame[i].Count;
                        foreach (ClientInfo client in clientsWaitingForGame[i])
                        {

                            if (!server.TestAndRemoveDisconnectedClients(client))
                            {
                                stillConnected--;
                            }

                            Console.WriteLine("DEBUG: client " + client.PlayerName + client.IPAddr + " connected is " + client.TcpClient.Client.Connected);
                        }
                        // If amount of current connected players is not sufficient to form a game, returns.
                        if (stillConnected < i) return;

                        // Place active players in to a list for message sending.
                        //      and remove disconnected players.
                        
                        GameSession newGameSession = new GameSession(idCounter++);
                        List<ClientInfo> readyToPlay = new List<ClientInfo>();
                        for (int playerId = 0; playerId < i; playerId++)
                        {
                            ClientInfo client;
                            bool dequeued;
                            do
                            {
                                dequeued = clientsWaitingForGame[i].TryDequeue(out client);
                            } while (!dequeued);

                            // Assign the ip address to a port
                            if (client.TcpClient.Client.Connected)
                            {
                                client.PlayerId = playerId;
                                client.ListeningPort = portNumber++;
                                //string clientPeerInfoMsg = client.IPAddr + " " + client.ListeningPort + " " + client.PlayerName + " " + client.PlayerId;

                                //playersToBeSent += clientPeerInfoMsg + ",";
                                //playersToBeSentList.Add(clientPeerInfoMsg);

                                readyToPlay.Add(client);
                                newGameSession.AddPlayer(client);

                            }
                            else
                            {
                                playerId--;
                            }

                        }

                        gameSessions.Add(newGameSession);

                        // Lastly, multicast the success response with game player data to the clients
                        responseMessage = ServerProgram.RESP_SUCCESS + " " + ServerProgram.REQ_GAME + " " + newGameSession.ToMessage();
                        Object listLock = new Object();
                        Parallel.ForEach(readyToPlay, (client) =>
                        {
                            Console.WriteLine("DEBUG: Response sent: " + responseMessage);
                            NetworkStream netStream = client.TcpClient.GetStream();

                            byte[] byteToSend = Encoding.ASCII.GetBytes(responseMessage);
                            netStream.Write(byteToSend, 0, byteToSend.Length);

                            Console.WriteLine("\nSent Acknowledgement");

                            lock (listLock)
                            {
                                server.ConnectedClients.Remove(server.ConnectedClients.Where(c => c.TcpClient == client.TcpClient).First());

                                client.TcpClient.Close();
                            }
                        });

                        //  Save game session in case a player need to reconnect to a game

                    }
                }
            }
        }

        public List<ConcurrentQueue<ClientInfo>> Queues
        {
            get { return clientsWaitingForGame; }
        }
        public GameSession[] GameSessions
        {
            get { return gameSessions.ToArray(); }
        }
    }

    public class GameSession
    {
        private int sessionId;
        private List<ClientInfo> players;

        public GameSession(int id)
        {
            sessionId = id;
            players = new List<ClientInfo>();
        }

        public bool ContainsPlayer(string pName)
        {
            return players.Contains(GetPlayer(pName));
        }

        public void RemovePlayer(string pName)
        {
            players.Remove(GetPlayer(pName));
        }

        public void AddPlayer(ClientInfo player)
        {
            players.Add(player);
        }

        public ClientInfo GetPlayer(string pName)
        {
            return players.Where(aplayer => aplayer.PlayerName == pName).First();
        }

        public string ToMessage()
        {
            string msg = "";
            foreach (ClientInfo c in players)
            {
                msg += c.IPAddr + " " + c.ListeningPort + " " + c.PlayerName + " " + c.PlayerId + ",";
            }
            return msg;
        }

        public int ID
        {
            get { return sessionId; }
        }

        public ClientInfo[] Players
        {
            get { return players.ToArray(); }
        }

    }
}
