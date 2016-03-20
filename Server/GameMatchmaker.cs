using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class GameMatchmaker
    { 
        private class GameSession
        {
            int sessionId;
            List<ClientInfo> players;

            public bool ContainsPlayer(string pName)
            {
                return players.Contains(players.Where(aplayer => aplayer.PlayerName == pName).First());
            }
        }


    }
}
