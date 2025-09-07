using System;

namespace YoloAbstractions;

public record Weight(
    decimal Price,
    decimal CarryFactor,
    DateTime Date,
    decimal MomentumFactor,
    string Ticker,
    decimal TrendFactor,
    decimal Volatility);
