using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server;

namespace Game.Tests
{
    /// <summary>
    /// This class will be responsible for testing replication manager in the server 
    /// It will use server interaction with the client to check that all info is replicated 
    /// correctly.
    /// </summary>
    [TestClass]
    public class ReplicationManagerTests
    {
        /// <summary>
        /// This property will be initialzie every time a test is run.
        /// It will be used as the server.
        /// </summary>
        private ServerProgram server { get; set; }

        /// <summary>
        /// This property will be initialzie every time a test is run.
        /// This will be used as the replication manager. This is the main
        /// property in this class. 
        /// </summary>
        private ReplicationManager replicationManager { get; set; }

        /// <summary>
        /// This method will be used for setting up the tests.
        /// it will most initialize different properties.
        /// </summary>
        [TestInitialize]
        private void Setup()
        {
            // Initialize server program that will be assoicated with the replication manager
            server = new ServerProgram();

            // associate replication manager with the server program initialized for the tests
            replicationManager = new ReplicationManager(server);

        }

        [TestMethod]
        private void test()
        {

        }

        [TestCleanup]
        private void Clean()
        {

        }


    }
}
