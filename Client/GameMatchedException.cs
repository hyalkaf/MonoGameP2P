using System;
using System.Runtime.Serialization;

namespace Client
{
    [Serializable]
    internal class GameMatchedException : Exception
    {
        public GameMatchedException()
        {
        }

        public GameMatchedException(string message) : base(message)
        {
        }

        public GameMatchedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GameMatchedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}