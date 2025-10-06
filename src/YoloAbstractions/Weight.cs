using System;

namespace YoloAbstractions;

public record Weight(string Ticker, decimal Value, DateTime TimeStamp)
{
    public static readonly Weight Empty = new(string.Empty, 0.0m, DateTime.MinValue);
}