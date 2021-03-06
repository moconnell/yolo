using System;

namespace YoloBroker;

public abstract class BrokerException : ApplicationException
{
    protected BrokerException(string message)
        : base(message)
    {
    }

    protected BrokerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}