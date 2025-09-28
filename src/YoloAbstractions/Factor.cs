using System;

namespace YoloAbstractions;

// public record Weight(
//     decimal Price,
//     decimal CarryFactor,
//     DateTime Date,
//     decimal MomentumFactor,
//     string Ticker,
//     decimal TrendFactor,
//     decimal Volatility);

public record Factor(
    string Id,
    FactorType Type,
    string Ticker,
    decimal RefPrice,
    decimal Value,
    DateTime TimeStamp);
