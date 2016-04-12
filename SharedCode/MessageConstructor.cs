using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedCode
{

    public class MessageConstructor
    {
        /// <summary>
        /// Need a lock in case message parts was changed by another thread
        /// </summary>
        private static Object messageLock = new object(); 

        /// <summary>
        /// This method construct a message to send seperator parts of the message using
        /// the passed seperator.
        /// </summary>
        /// <param name="messageParts">Components of message that will concanted toether.</param>
        /// <param name="seperator">Seperator in between components of message.</param>
        /// <param name="suffixForMessage">This parameter will be used to include at the end of a message. It will an two lines string be default.</param>
        /// <returns></returns>
        public static string ConstructMessageToSend(List<string> messageParts, string seperator = " ", string suffixForMessage = "\n\n")
        {
            lock(messageLock)
            {
                string message = string.Empty;

                // append all components of message together
                for (int i = 0; i < messageParts.Count; i++)
                {
                    // seperator shouldn't be added at the end of the message
                    if (i != messageParts.Count - 1)
                    {
                        message += messageParts[i] + seperator;
                    }
                    else
                    {
                        message += messageParts[i];
                    }

                }

                // Append suffix for tcp messages
                message += suffixForMessage;

                return message;
            }
        }
    }
}
