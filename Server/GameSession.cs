using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
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
