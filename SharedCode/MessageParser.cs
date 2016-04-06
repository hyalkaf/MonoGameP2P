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
    /// Get the first occurence keyword in a message.
    /// </summary>
    /// <param name="msg"> The message to parse </param>
    /// <param name="first"> First occurence </param>
    /// <param name="rest"> Rest of the message </param>
    /// <param name="separator"></param>
    public static void ParseNext(string msg, out string first, out string rest, char separator = ' '){
        lock (messageLock)
        {
            msg = msg.Trim();
            msg = msg.Replace('\t', ' ');
            rest = "";
            if (msg.IndexOf(separator) == -1)
            {
                first = msg;
            }

            else
            {
                first = msg.Substring(0, msg.IndexOf(separator)).Trim();
                rest = msg.Substring(first.Length).Trim();
            }
        }
    }
}
