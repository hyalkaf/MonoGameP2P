using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedCode
{
    /// <summary>
    /// This class defines communication messages that will be used
    /// to communicate between different parts of the program.
    /// </summary>
    public class CommunicationProtocol
    {
        public static class Server
        {
            /* Request from client*/
            public const string GAME = "game";
            public const string PLAYERS = "players";
            public const string CANCEL = "cancel";
            public const string CHECKNAME = "checkname";
            public const string RECONN = "reconn";
            public const string SERVRECONN = "servreconn";

            /* Request from in-game peer*/
            public const string RMPLAYER = "rmplayer";
        }
        
        public static class Peers
        {

        }

        public static class ReplicationManagers
        {

        }

    }
}
