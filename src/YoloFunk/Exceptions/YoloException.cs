using System;

namespace YoloFunk
{
    public class YoloException : ApplicationException
    {
        public YoloException(string message)
            : base(message)
        {
        }

        public YoloException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}