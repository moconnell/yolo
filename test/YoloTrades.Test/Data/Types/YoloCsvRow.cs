using CsvHelper.Configuration.Attributes;

namespace YoloTrades.Test.Data.Types;

public class YoloCsvRow
{
    [Name("Ticker")]
    public required string Ticker { get; set; }

    [Name("Price, $")]
    public decimal Price { get; set; }

    [Name("Momentum")]
    public decimal Momentum { get; set; }

    [Name("Trend")]
    public decimal Trend { get; set; }

    [Name("Carry")]
    public decimal Carry { get; set; }

    [Name("Vol")]
    public decimal Volatility { get; set; }

    [Name("Unconstrained Target Weight")]
    public decimal UnconstrainedTTargetWeight { get; set; }

    [Name("Vol Scaled Target Weight")]
    public decimal VolScaledTargetWeight { get; set; }

    [Name("Current Position")]
    public decimal CurrentPosition { get; set; }

    [Name("Current Position Value, $")]
    public decimal CurrentPositionValue { get; set; }

    [Name("Current Weight")]
    public decimal CurrentWeight { get; set; }

    [Name("Trades (Qty)")]
    public decimal TradeQuantity { get; set; }

    [Name("Trade Value, $")]
    public decimal TradeValue { get; set; }

    [Name("Post-Trade Positions")]
    public decimal PostTradePosition { get; set; }

    [Name("Post-Trade Position Value, $")]
    public decimal PostTradePositionValue { get; set; }

    [Name("Post-Trade Weights")]
    public decimal PostTradeWeight { get; set; }

    [Name("Diff to Constrained Target Weight Before Trade")]
    public required string DiffToConstrainedTargetWeightPreTrade { get; set; }

    [Name("Diff to Constrained Target Weight After Trade")]
    public required string DiffToConstrainedTargetWeightPostTrade { get; set; }

    [Name("OK?")]
    [BooleanTrueValues("YES")]
    [BooleanFalseValues("NO")]
    public bool Ok { get; set; }
}