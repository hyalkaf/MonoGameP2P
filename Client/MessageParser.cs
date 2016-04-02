using System;


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
    /// <param name="separator"></param>
    public static void ParseNext(string msg, out string first, out string rest, char separator = ' ')
    {
        lock (messageLock) { 
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
