using System;

namespace YoloAbstractions;

public record Factor(
    string Id,
    FactorType Type,
    string Ticker,
    decimal? RefPrice,
    decimal Value,
    DateTime TimeStamp);
