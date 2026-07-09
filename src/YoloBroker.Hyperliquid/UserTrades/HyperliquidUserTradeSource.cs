using System.Text.Json;
using HyperLiquid.Net.Interfaces.Clients;
using HyperLiquid.Net.Objects.Models;
using YoloAbstractions;
using YoloBroker.Hyperliquid.Config;
using YoloBroker.Interface;

namespace YoloBroker.Hyperliquid.UserTrades;

public sealed class HyperliquidUserTradeSource(
    IHyperLiquidRestClient hyperliquidClient,
    HyperliquidConfig config) : IUserTradeSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<UserTradeRecord>> GetUserTradesByTimeAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        var address = string.IsNullOrWhiteSpace(config.VaultAddress)
            ? config.Address
            : config.VaultAddress;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Hyperliquid trade ingestion requires a wallet or vault address.");
        }

        var result = await hyperliquidClient.FuturesApi.Trading.GetUserTradesByTimeAsync(
            startUtc.UtcDateTime,
            endUtc.UtcDateTime,
            aggregateByTime: false,
            address,
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Could not download Hyperliquid user trades: {result.Error}");
        }

        return result.Data.Select(ToRecord).ToArray();
    }

    private static UserTradeRecord ToRecord(HyperLiquidUserTrade trade)
    {
        var timestampUtc = ToUtc(trade.Timestamp);

        return new UserTradeRecord(
            Exchange.Hyperliquid,
            DecimalToString(trade.ClosedPnl),
            trade.ExchangeSymbol,
            trade.Symbol,
            trade.SymbolType.ToString(),
            trade.Crossed,
            trade.Direction.ToString(),
            trade.Hash,
            trade.OrderId,
            DecimalToString(trade.Price),
            trade.OrderSide.ToString(),
            DecimalToString(trade.StartPosition),
            DecimalToString(trade.Quantity),
            timestampUtc,
            DecimalToString(trade.Fee),
            trade.FeeToken,
            DecimalToString(trade.BuilderFee),
            trade.TradeId,
            trade.Liquidation is null ? null : JsonSerializer.Serialize(trade.Liquidation, JsonOptions),
            trade.TwapId,
            trade.ClientOrderId,
            JsonSerializer.Serialize(trade, JsonOptions));
    }

    private static DateTimeOffset ToUtc(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Utc
            ? timestamp
            : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        return new DateTimeOffset(utc);
    }

    private static string DecimalToString(decimal value) => value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);

    private static string? DecimalToString(decimal? value) =>
        value?.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
}
