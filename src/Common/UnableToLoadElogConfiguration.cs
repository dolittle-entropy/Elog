using System.Runtime.Serialization;

namespace Common
{
    [Serializable]
    public class UnableToLoadElogConfiguration : Exception
    {
        public UnableToLoadElogConfiguration()
        {
        }

        public UnableToLoadElogConfiguration(string? message) : base(message)
        {
        }

        public UnableToLoadElogConfiguration(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected UnableToLoadElogConfiguration(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
