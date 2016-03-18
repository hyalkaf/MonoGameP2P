using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server;
using System.Net;

namespace dummy
{
    class Program
    {
        static void Main(string[] args)
        {
            ReplicationManager rm = new ReplicationManager();

            Dictionary<string, string> gameSession = new Dictionary<string, string>();
            gameSession.Add("1", "19.22.33.44 8000 hdjas 0,19.22.33.44 8000 jlkds 1");

            string response = rm.ConstructPrimaryMessageSession(gameSession);

            Console.WriteLine(response);

            string session = "session";

            string responsefinal = rm.ConstructReplicaMessageAfterReceivingServerInfo(session, response.Substring(session.Length).Trim());

            Console.WriteLine(responsefinal);

            Console.ReadLine();

        }
    }
}
