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
            public static class Request
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
            /// <summary>
            /// Main response message type
            /// 
            /// </summary>
            public static class Response
            {
                public const string SUCCESS = "success";
                public const string FAILURE = "failure";
                public const string ERROR = "error";
            }
        }
        
        public static class Peers
        {
            /// <summary>
            /// Main request message type
            /// </summary>
            public static class Request
            {
                /*Request to other peers*/
                public const string CHANGEIP = "changeip";
                public const string QUIT = "quit";
                public const string RECONNECTED = "reconnected";
                public const string STRIKE = "strike";
                public const string TIMEUPDATE = "timeupdate";
                public const string TURN = "turn";
                public const string WHOISLEADER = "whoisleader";
                public const string HANDSHAKE = "handshake";

            }

            /// <summary>
            /// Main response message type
            /// </summary>
            public static class Response
            {
                public const string SUCCESS = "success";
                public const string FAILURE = "failure";
                public const string ERROR = "error";
                public const string UNKNOWN = "unknownrequest";
                public const string NOLEADER = "noleader";
            }
        }

        public static class ReplicationManagers
        {

        }

    }
}
