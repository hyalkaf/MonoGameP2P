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

        /// <summary>
        /// Iniailize a new MatchMaker object containing Game Sessions and Clients Waiting for Games of different capacities.
        /// </summary>
        public GameMatchmaker() {
            gameSessions = new List<GameSession>();
            clientsWaitingForGame = new List<ConcurrentQueue<ClientInfo>>();
        }

        /// <summary>
        /// Add a new Game session to the list of game sessions
        /// </summary>
        /// <param name="gs">Game session to be added</param>
        public void NewGameSession(GameSession gs)
        {
            gameSessions.Add(gs);
        }

        /// <summary>
        /// Remove a game session from game sessions. 
        /// </summary>
        /// <param name="id"></param>
        public void RemoveGameSession(int id)
        {
            // TODO: Deal with the case when the lambda doesn't return anything -> exception will be thrown
            gameSessions.Remove(gameSessions.Where(gs => gs.ID == id).First());
        }

        /// <summary>
        /// This method returns a speific game session given the ID of that session
        /// </summary>
        /// <param name="id">ID of the game session passed.</param>
        /// <returns>a specific game session with the passed id of that game session.</returns>
        public GameSession GetGameSession(int id)
        {

            return gameSessions.Find(gs => gs.ID == id);
        }

        /// <summary>
        /// This method finds out the index of the player in the list. 
        /// </summary>
        /// <param name="playername">Player name to find queue the player is in</param>
        /// <returns>Index of the player in the List or -1 if it doesn't exist</returns>
        public int IsInQueue(string playername)
        {
            // For every Queue in the list of clients waiting for a game
            for (int i = 0; i < clientsWaitingForGame.Count; i++)
            {
                // For every client Info in this specific queue
                ConcurrentQueue<ClientInfo> q = clientsWaitingForGame[i];
                foreach (ClientInfo ci in q)
                {
                    // Check if player is in this queue
                    if (ci.PlayerName == playername)
                    {
                        return i;
                    }
                }
               
            }

            // Return -1 if player is not in the list of players waiting to get into game.
            return -1;

        }

        /// <summary>
        /// This method Add players to a specific queue (their game capacity).
        /// </summary>
        /// <param name="player">Player to be added.</param>
        /// <param name="queueNum">Queue number (game capacity) that the player will be added to.</param>
        public void AddPlayerToQueue(ClientInfo player, int queueNum)
        {
            // Check that queue number exists in the list else add more queues to the list equal to the queueNumber.
            if (queueNum >= clientsWaitingForGame.Count)
            {
                for (int i = clientsWaitingForGame.Count; i <= queueNum; i++)
                {
                    clientsWaitingForGame.Add(new ConcurrentQueue<ClientInfo>());
                }
            }

            // Add player to the specific queue
            clientsWaitingForGame[queueNum].Enqueue(player);

        }

        /// <summary>
        /// Converts Game sessions to a string delimited newlines between each game sessio, 
        /// by spaces between player info and comma between each player's info.
        /// </summary>
        /// <returns>A string containing all this game session info.</returns>
        public string GameSessionsToMessage()
        {
            string message = string.Empty;

            // For every game session convert it to a message
            for (int gameSessionIndex = 0; gameSessionIndex < GameSessions.Count(); gameSessionIndex++)
            {
                // Last game session will have two newlines after it
                if (gameSessionIndex.Equals(GameSessions.Count() - 1))
                {
                    message += GameSessions[gameSessionIndex].ToMessage() + "\n\n";
                }
                else
                {
                    message += GameSessions[gameSessionIndex].ToMessage() + "\n";
                }
            }

            return message;
        }

        /// <summary>
        /// Converts Game sessions to a string delimited newlines between each game sessio, 
        /// by spaces between player info and comma between each player's info.
        /// </summary>
        /// <returns>A string containing all this game session info.</returns>
        public string ClientsWaitingForGameToMessage()
        {
            string message = string.Empty;

            // For game requests of specific index in the list convert all info in the queue to a string
            for (int clientsWaitingIndex = 0; clientsWaitingIndex < clientsWaitingForGame.Count(); clientsWaitingIndex++)
            {
                for (int clientInQueueIndex = 0; clientInQueueIndex < clientsWaitingForGame[clientsWaitingIndex].Count; clientInQueueIndex++)
                {
                    // If element is last, then append comma to the end as a delimiter for different game capacities.
                    if (clientInQueueIndex.Equals(clientsWaitingForGame[clientsWaitingIndex].Count - 1))
                    {
                        message += clientsWaitingForGame[clientsWaitingIndex].ElementAt(clientInQueueIndex).ToMessage() + ",";
                    }
                    // If element is first, message will have the capacity first then each player delimited by spaces.
                    else if (clientInQueueIndex.Equals(0))
                    {
                        message += clientsWaitingIndex + " " + clientsWaitingForGame[clientsWaitingIndex].ElementAt(clientInQueueIndex).ToMessage() + " ";
                    }
                    else
                    {
                        message += clientsWaitingForGame[clientsWaitingIndex].ElementAt(clientInQueueIndex).ToMessage() + " ";
                    }

                }
            }

            return message;
        }

        /// <summary>
        /// This method is responsible for matching players to a game.
        /// </summary>
        /// <param name="server">Server Progam currently being used (primary)</param>
        public void MatchPeers(ServerProgram server)
        {
            string responseMessage = string.Empty;

            // bypass first and second index since there are no matches with 0 or 1 player
            for (int i = 2; i < clientsWaitingForGame.Count; i++)
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

        /// <summary>
        /// Getter for list of queues of clients waiting for game.
        /// </summary>
        public List<ConcurrentQueue<ClientInfo>> ClientGameQueue
        {
            get { return clientsWaitingForGame; }
            set { clientsWaitingForGame = value; }
        }

        /// <summary>
        /// Number of clients waiting for game in the whole list.
        /// </summary>
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

        /// <summary>
        /// Getter and setter for game sessions as array
        /// </summary>
        public GameSession[] GameSessions
        {
            get { return gameSessions.ToArray(); }
            set { gameSessions = value.ToList(); }
        }

        

    }

    /// <summary>
    /// This class is keeping track of game sessions
    /// These are games currently being player.
    /// It has an ID and a list of players with their Information
    /// This information is Player IP, Port Number, Player Name, Player ID.
    /// </summary>
    public class GameSession
    {
        private int sessionId;
        private List<ClientInfo> players;

        /// <summary>
        /// Create a new game session with this ID.
        /// </summary>
        /// <param name="id"></param>
        public GameSession(int id)
        {
            sessionId = id;
            players = new List<ClientInfo>();
        }

        /// <summary>
        /// This method check if game session has this player or not.
        /// </summary>
        /// <param name="pName">Player to be checked against this game session</param>
        /// <returns>False if player doesn't exist and true if it does</returns>
        public bool ContainsPlayer(string pName)
        {
            if (GetPlayer(pName) == null)
                return false;

            return true;
        }

        /// <summary>
        /// Removes a player from this game session.
        /// </summary>
        /// <param name="pName">Player to be removed</param>
        public void RemovePlayer(string pName)
        {
            players.Remove(GetPlayer(pName));
        }

        /// <summary>
        /// Adds a player to this game session
        /// </summary>
        /// <param name="player">Player to be added</param>
        public void AddPlayer(ClientInfo player)
        {
            players.Add(player);
        }

        /// <summary>
        /// This method gets a player from this game session.
        /// </summary>
        /// <param name="pName">Player name of the player to be returned.</param>
        /// <returns>PlayerInfo to be returned as a ClientInfo Object.</returns>
        public ClientInfo GetPlayer(string pName)
        {
            return players.Find(aplayer => aplayer.PlayerName == pName);
        }

        /// <summary>
        /// Converts Game session to a string delimited by spaces between player info and comma between each player's info.
        /// </summary>
        /// <returns>A string containing all this game session info.</returns>
        public string ToMessage()
        {
            string msg = "";
            for (int clientInfoIndex = 0; clientInfoIndex < players.Count; clientInfoIndex++)
            {
                // Add ID in the first plyaer info string delimied by spaces
                // Don't add comma at the end of the string
                if (clientInfoIndex == 0)
                {
                    msg += ID + " " + players[clientInfoIndex].ToMessage() + ",";
                }
                else if (clientInfoIndex.Equals(players.Count - 1))
                {
                    msg += players[clientInfoIndex].ToMessage();
                }
                else
                {
                    msg += players[clientInfoIndex].ToMessage() + ",";
                }
            }

            return msg;
        }

        /// <summary>
        /// ID of this game session.
        /// </summary>
        public int ID
        {
            get { return sessionId; }
        }

        /// <summary>
        /// Players in this game seesion.
        /// </summary>
        public ClientInfo[] Players
        {
            get { return players.ToArray(); }
        }

        public List<ClientInfo> SetPlayers
        {
            set { players = value; }
        }

    }
}
