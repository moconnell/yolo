namespace YoloRuntime;

public abstract class RuntimeException : ApplicationException
{
    protected RuntimeException(string message) : base(message)
    {
    }
}