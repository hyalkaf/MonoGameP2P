using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Message helper class to parse incoming or outgoing message
/// </summary>
public class MessageParser
{

    private static Object messageLock = new object();

    /// <summary>
    /// Get the first occurence keyword in a message as well as rest of message.
    /// First and rest will be returned as references. 
    /// </summary>
    /// <param name="msg"> The message to parse </param>
    /// <param name="first"> First occurence </param>
    /// <param name="rest"> Rest of the message </param>
    /// <param name="separator">seperator for the message and space by default</param>
    public static void ParseNext(string msg, out string first, out string rest, char separator = ' '){
        // need message not to change so we use a lock
        lock (messageLock)
        {
            //  Trim message
            msg = msg.Trim();

            // replace tabs with white spaces
            msg = msg.Replace('\t', ' ');
            rest = "";

            // in case there is no occurence of seperator first and rest are the same
            if (msg.IndexOf(separator) == -1)
            {
                first = msg;
            }

            else
            {
                // First will be string up to the seperator while rest is 
                // the rest after the seperator.
                first = msg.Substring(0, msg.IndexOf(separator)).Trim();
                rest = msg.Substring(first.Length).Trim();
            }
        }
    }
}
