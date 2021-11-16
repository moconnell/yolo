using System;

namespace YoloAbstractions.Config
{
    public class ConfigException : ApplicationException
    {
        public ConfigException(string message)
            : base(message)
        {
        }

        public ConfigException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}