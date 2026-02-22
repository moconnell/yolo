using YoloAbstractions;

namespace YoloTrades;

public record ProjectedPosition(MarketInfo Market, decimal Position, decimal Nominal, IEnumerable<Trade>? Trades = null)
{
    public decimal ProjectedAmount => Position + (Trades?.Sum(t => t.Amount) ?? 0);

    public decimal? ProjectedWeight => CurrentWeight + (Trades?.Sum(CalcWeight) ?? 0);

    public bool HasPosition => ProjectedAmount != 0;

    private decimal? CurrentWeight => CalcWeight(Position);

    public static ProjectedPosition operator +(ProjectedPosition position, Trade trade) =>
        new(
            position.Market,
            position.Position,
            position.Nominal,
            new List<Trade>(position.Trades ?? []) { trade });

    private decimal? CalcWeight(Trade t) => CalcWeight(t.Amount);

    private decimal? CalcWeight(decimal amount)
    {
        if (amount == 0)
            return 0;

        var price = amount >= 0 ? Market.Bid : Market.Ask;
        return price is null ? null : amount * price.Value / Nominal;
    }
}
