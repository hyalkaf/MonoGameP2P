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
            public static class Request
            {
                // Request and response messsages between backup servers and primary server
                // These are mentioned in our design document
                // REQ_BACKUP is a request message that will be sent whenever a new backup is initialized
                // It will be sending this request message with its own IP address to the primary.
                public static readonly string REQ_BACKUP = "backup";

                // REQ_NAMES is a request message that will be sent from backup to primary.
                // Asking for names 
                public static readonly string REQ_NAMES = "nameRequest";

                // REQ_GAMESESSIONS is a request message for game session and 
                public static readonly string REQ_GAMESESSIONS = "sessionRequest";

                // REQ_CHECK is a request message from backup replication manager to the primary server 
                // checking if it still exists and it can't receive and respond to messages.
                public static readonly string REQ_CHECK = "check";

                // REQ_MATCH is a request message to server asking them  for game queue
                public static readonly string REQ_MATCH = "matchesRequest";

                // REQ_UPDATE_BACKUP is a request that will be sent after a new primary is elected.
                // This request holds the new information about the backup servers currently existing
                // in the local network. 
                public static readonly string REQ_UPDATE_BACKUP = "update-backup";
            }

            public static class Response
            {
                // REQ_ADDRESSES is a response message from primary servers to backup servers 
                // sending them information about addresses of all backup server currently in 
                // pool of servers. This will be sent after a new backup server have entered the pool
                // and have already sent its own ip address with a REQ_BACK.
                public static readonly string RES_ADDRESSES = "address";

                // RES_NAMES is a response message with player names for REQ_NAMES
                public static readonly string RES_NAMES = "playerNames";

                // RES_GAMESESSIONS is a response message for game sessions information
                public static readonly string RES_GAMESESSIONS = "gameSessions";

                // RES_MATCH is a response message with game queue info
                public static readonly string RES_MATCH = "matchesResponse";
            }
        }

    }
}
