using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedCode
{

    public class MessageConstructor
    {
        private static Object messageLock = new object(); 

        public static string ConstructMessageToSend(List<string> messageParts, string seperator = " ")
        {
            lock(messageLock)
            {
                string message = string.Empty;

                // Send backup servers ip addresses starting from first backup server exculding primary server
                for (int i = 0; i < messageParts.Count; i++)
                {
                    // Comma shouldn't be added at the end of the message
                    if (i != messageParts.Count - 1)
                    {
                        message += messageParts[i] + seperator;
                    }
                    else
                    {
                        message += messageParts[i];
                    }

                }

                message += "\n\n";

                return message;
            }
        }
    }
}
