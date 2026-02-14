using YoloAbstractions;

namespace YoloTrades;

public record ProjectedPosition(MarketInfo Market, decimal Position, decimal Nominal, IEnumerable<Trade>? Trades = null)
{
    public decimal ProjectedAmount => Position + (Trades?.Sum(t => t.Amount) ?? 0);

    public decimal? ProjectedWeight => CurrentWeight + (Trades?.Sum(CalcWeight) ?? 0);

    public bool HasPosition => ProjectedWeight != 0;

    private decimal? CurrentWeight => CalcWeight(Position);

    public static ProjectedPosition operator +(ProjectedPosition position, Trade trade) =>
        new(
            position.Market,
            position.Position,
            position.Nominal,
            new List<Trade>(position.Trades ?? []) { trade });

    private decimal? CalcWeight(Trade t) => CalcWeight(t.Amount);

    private decimal? CalcWeight(decimal amount) => amount * (amount >= 0 ? Market.Bid : Market.Ask) / Nominal;
}