using System;
using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

namespace YoloTrades;

public record ProjectedPosition(MarketInfo Market, decimal Position, decimal Nominal, IEnumerable<Trade>? Trades = null)
{
    public decimal? ProjectedWeight => CurrentWeight + (Trades?.Sum(CalcWeight) ?? 0);

    public bool HasPosition => ProjectedWeight != 0;

    private decimal? CurrentWeight => CalcWeight(Position);

    public static ProjectedPosition operator +(ProjectedPosition position, Trade trade) =>
        position with { Trades = new List<Trade>(position.Trades ?? Array.Empty<Trade>()) { trade } };

    private decimal? CalcWeight(Trade t) => CalcWeight(t.Amount);

    private decimal? CalcWeight(decimal amount) => amount * (amount >= 0 ? Market.Bid : Market.Ask) / Nominal;
}