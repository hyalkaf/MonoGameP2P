using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
        
    class ReplicationManager
    {
        public static string primaryServerIp = "10.13.136.75";
        public static List<ServerProgram> listReplicas = new List<ServerProgram>();

        private TcpListener rmListener;
        public ReplicationManager(ServerProgram replica)
        {
            addReplica(replica);
           // rmListener = new TcpListener();

        }

        public void addReplica(ServerProgram replica)
        {
            listReplicas.Add(replica);
        }

        public void SendReplica()
        {
  
        }

        public void ListenReplica()
        {

        }

        public void MakeThisServerPrimary()
        {

        }

        public bool IsPrimary(string ipAddr)
        {
            return ipAddr == primaryServerIp;
        }

        
    }
}
