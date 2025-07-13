using System;

namespace YoloWeights.Exceptions;

public class WeightsException : ApplicationException
{
    public WeightsException(string message)
        : base(message)
    {
    }

    public WeightsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}