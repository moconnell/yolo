namespace YoloBroker.Exceptions;

public class BrokerException : ApplicationException
{
    public BrokerException(string message)
        : base(message)
    {
    }

    public BrokerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}