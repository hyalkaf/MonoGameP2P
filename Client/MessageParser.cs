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
    /// <param name="msg"></param>
    /// <param name="first"></param>
    /// <param name="rest"></param>
    public static void ParseNext(string msg, out string first, out string rest)
    {
        lock (messageLock) { 
            msg = msg.Trim();
            msg = msg.Replace('\t', ' ');
            rest = "";
            if (msg.IndexOf(" ") == -1)
            {
                first = msg;
            }

            else
            {
                first = msg.Substring(0, msg.IndexOf(" ")).Trim();
                rest = msg.Substring(first.Length).Trim();
            }
        }
    }
}
