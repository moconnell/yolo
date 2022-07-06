using System;
using System.Linq;

namespace YoloAbstractions;

public record Weight(
    decimal Price,
    decimal ComboWeight,
    DateTime Date,
    decimal MomentumFactor,
    string Ticker,
    decimal TrendFactor)
{
    public string BaseAsset => Ticker.Split('/').First();
}