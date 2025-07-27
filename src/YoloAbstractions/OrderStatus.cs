namespace YoloAbstractions;

public enum OrderStatus
{
    NotSet = 0,
    Open,
    Filled,
    Canceled,
    Triggered,
    Rejected,
    MarginCanceled,
    WaitingFill,
    WaitingTrigger
}