using YoloAbstractions;

namespace YoloRuntime;

public class OpenOrdersException : RuntimeException
{
    public OpenOrdersException(string message, IEnumerable<Order> orders) : base(message) => Orders = orders;

    public IEnumerable<Order> Orders { get; }
}