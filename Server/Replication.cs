using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
        
    class ReplicationManager
    {
        public static IPAddress primaryServerIp = IPAddress.Parse("0.0.0.0");
        public static List<ServerProgram> listReplicas = new List<ServerProgram>();

        private TcpListener rmListener;
        public ReplicationManager(ServerProgram replica)
        {
            addReplica(replica);
            // rmListener = new TcpListener();
            if (replica.isPrimaryServer)
            {
                primaryServerIp = replica.thisLocalAddr;
            }
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

        public bool IsPrimary(IPAddress ipAddr)
        {
            return ipAddr == primaryServerIp;
        }

        
    }
}
