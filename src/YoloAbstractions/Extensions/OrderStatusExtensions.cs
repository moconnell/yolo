namespace YoloAbstractions.Extensions;

public static class OrderStatusExtensions
{
    public static bool IsTerminalStatus(this OrderStatus status)
    {
        return status is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.Rejected or OrderStatus.MarginCanceled;
    }
}
