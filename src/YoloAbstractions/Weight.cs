using System;

namespace YoloAbstractions
{
    public record Weight(
        decimal Price,
        decimal ComboWeight,
        DateTime Date,
        decimal MomentumFactor,
        string Ticker,
        decimal TrendFactor);
}