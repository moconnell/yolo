using System;

namespace YoloWeights.Exceptions;

public class ApiException : ApplicationException
{
    public ApiException(string message)
        : base(message)
    {
    }

    public ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}