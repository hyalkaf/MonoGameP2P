using System;
using System.Collections;
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
        private Object queueLock = new Object();

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

            return gameSessions.Find(gs => gs.ID == id);
        }

        public int IsInQueue(string playername)
        {
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                ConcurrentQueue<ClientInfo> q = clientsWaitingForGame[i];
                foreach (ClientInfo ci in q)
                {
                    if (ci.PlayerName == playername)
                    {
                        return i;
                    }
                }
               
            }
            return -1;

        }

        //public int RemovePlayerFromQueue(string playername)
        //{
        //    lock (queueLock)
        //    {
        //        for (int i = 0; i < clientsWaitingForGame.Count; i++)
        //        {
        //            ConcurrentQueue<ClientInfo> q = clientsWaitingForGame[i];
        //            List<ClientInfo> lstQueue = q.ToList();
        //            ClientInfo aPlayer = lstQueue.Find(ci => ci.PlayerName == playername);
        //            if (aPlayer != null) {
        //                lstQueue.Remove(aPlayer);
        //                clientsWaitingForGame[i] = new ConcurrentQueue<ClientInfo>(lstQueue);
        //                return i;
        //            }
        //        }
        //    }
        //    return -1;

        //}

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
            for (int i = 2; i < clientsWaitingForGame.Count; i++)
            {
                // bypass first and second index since there are no matches with 0 or 1 player
               
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

                            newGameSession.AddPlayer(client);

                        }
                        else
                        {
                            playerId--;
                        }

                    }

                    gameSessions.Add(newGameSession);

                    // Lastly, multicast the success response with game player data to the clients
                    responseMessage = ServerProgram.Response.SUCCESS + " " + ServerProgram.Request.GAME + " " + newGameSession.ToMessage();
                    Object listLock = new Object();
                    Parallel.ForEach(newGameSession.Players, (client) =>
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
                }
            }
        }

        public List<ConcurrentQueue<ClientInfo>> Queues
        {
            get { return clientsWaitingForGame; }
        }

        public int NumOfClientsInQueue
        {
            get {
                int total = 0;
                foreach(ConcurrentQueue<ClientInfo> q in clientsWaitingForGame)
                {
                    total += q.Count;
                }
                return total;
            }
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

        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public bool ContainsPlayer(string pName)
        {
            if (GetPlayer(pName) == null)
                return false;

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pName"></param>
        public void RemovePlayer(string pName)
        {
            players.Remove(GetPlayer(pName));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="player"></param>
        public void AddPlayer(ClientInfo player)
        {
            players.Add(player);
        }

        public ClientInfo GetPlayer(string pName)
        {
            return players.Find(aplayer => aplayer.PlayerName == pName);
        }

        public string ToMessage()
        {
            string msg = "";
            foreach (ClientInfo c in players)
            {
                msg += c.IPAddr + " " + c.ListeningPort + " " + c.PlayerName + " " + c.PlayerId + " " + ID + ",";
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
