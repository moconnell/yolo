using CsvHelper;

namespace YoloTrades.Test.Data.Types;

[CsvRecord]
public partial class YoloCsvRow
{
    [CsvColumn("Ticker")]
    public required string Ticker { get; set; }

    [CsvColumn("Price, $")]
    public decimal Price { get; set; }

    [CsvColumn("Momentum")]
    public decimal Momentum { get; set; }

    [CsvColumn("Trend")]
    public decimal Trend { get; set; }

    [CsvColumn("Carry")]
    public decimal Carry { get; set; }

    [CsvColumn("Vol")]
    public decimal Volatility { get; set; }

    [CsvColumn("Unconstrained Target Weight")]
    public decimal UnconstrainedTTargetWeight { get; set; }

    [CsvColumn("Vol Scaled Target Weight")]
    public decimal VolScaledTargetWeight { get; set; }

    [CsvColumn("Current Position")]
    public decimal CurrentPosition { get; set; }

    [CsvColumn("Current Position Value, $")]
    public decimal CurrentPositionValue { get; set; }

    [CsvColumn("Current Weight")]
    public decimal CurrentWeight { get; set; }

    [CsvColumn("Trades (Qty)")]
    public decimal TradeQuantity { get; set; }

    [CsvColumn("Trade Value, $")]
    public decimal TradeValue { get; set; }

    [CsvColumn("Post-Trade Positions")]
    public decimal PostTradePosition { get; set; }

    [CsvColumn("Post-Trade Position Value, $")]
    public decimal PostTradePositionValue { get; set; }

    [CsvColumn("Post-Trade Weights")]
    public decimal PostTradeWeight { get; set; }

    [CsvColumn("Diff to Constrained Target Weight Before Trade")]
    public required string DiffToConstrainedTargetWeightPreTrade { get; set; }

    [CsvColumn("Diff to Constrained Target Weight After Trade")]
    public required string DiffToConstrainedTargetWeightPostTrade { get; set; }

    [CsvColumn("OK?")]
    [CsvBooleanValues("YES", "NO")]
    public bool Ok { get; set; }
}