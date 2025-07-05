using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using DynamicData;
using YoloAbstractions;
using YoloAbstractions.Extensions;

namespace YoloKonsole;

public static class DataTableExtensions
{
    private const string Token = nameof(Token);
    private const string Instrument = nameof(Instrument);
    private const string Side = nameof(Side);
    private const string Amount = nameof(Amount);
    private const string Limit = nameof(Limit);
    private const string Value = nameof(Value);
    private const string PostOnly = "Post-Only";
    private const string Created = nameof(Created);
    private const string Status = nameof(Status);
    private const string Remaining = nameof(Remaining);
    private const string Position = nameof(Position);

    public static DataTable AsDataTable(
        this IObservable<IChangeSet<
            KeyValuePair<string, (IReadOnlyDictionary<string, TradeResult>, IReadOnlyDictionary<string, Position>)>,
            string>> observable,
        CancellationToken cancellationToken)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(Token, typeof(string));
        dataTable.Columns.Add(Instrument, typeof(string));
        dataTable.Columns.Add(Position, typeof(decimal));
        dataTable.Columns.Add(Side, typeof(string));
        dataTable.Columns.Add(Amount, typeof(decimal));
        dataTable.Columns.Add(Limit, typeof(decimal));
        dataTable.Columns.Add(Value, typeof(decimal));
        dataTable.Columns.Add(PostOnly, typeof(bool));
        dataTable.Columns.Add(Created, typeof(DateTime));
        dataTable.Columns.Add(Status, typeof(OrderStatus));
        dataTable.Columns.Add(Remaining, typeof(decimal));

        dataTable.PrimaryKey = new[] { dataTable.Columns[1] };

        bool SetRow(DataRow? dataRow, IReadOnlyList<object?> objects)
        {
            if (dataRow is null)
            {
                return false;
            }

            dataRow.BeginEdit();
            dataRow[Token] = objects[0];
            dataRow[Instrument] = objects[1];
            dataRow[Position] = objects[2];
            dataRow[Side] = objects[3];
            dataRow[Amount] = objects[4];
            dataRow[Limit] = objects[5];
            dataRow[Value] = objects[6];
            dataRow[PostOnly] = objects[7];
            dataRow[Created] = objects[8] ?? DBNull.Value;
            dataRow[Status] = objects[9] ?? DBNull.Value;
            dataRow[Remaining] = objects[10] ?? DBNull.Value;
            dataRow.EndEdit();

            return true;
        }

        void OnNext(IChangeSet<KeyValuePair<string, (IReadOnlyDictionary<string, TradeResult> tradeResults, IReadOnlyDictionary<string, Position> positions)>, string> changeSet)
        {
            foreach (var change in changeSet)
            {
                var baseAsset = change.Key;
                var positions = change.Current.Value.positions;
                var tradeResults = change.Current.Value.tradeResults;
                var keys = new List<string>();

                foreach (var kvp in tradeResults)
                {
                    var tradeResult = kvp.Value;
                    var assetName = tradeResult.Trade.AssetName;
                    keys.Add(assetName);
                    var position = positions[assetName];

                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                        case ChangeReason.Refresh:
                            var row = dataTable.Rows.Find(assetName);
                            var itemArray = tradeResult.ToArray(position);
                            if (SetRow(row, itemArray))
                            {
                                break;
                            }

                            var row2 = dataTable.Select($"{Token} = '{baseAsset}'").FirstOrDefault();
                            if (SetRow(row2, itemArray))
                            {
                                break;
                            }

                            dataTable.Rows.Add(itemArray);

                            break;
                        case ChangeReason.Moved:
                            break;
                        case ChangeReason.Remove:
                            var row2Delete = dataTable.Rows.Find(assetName);
                            if (row2Delete is { })
                            {
                                dataTable.Rows.Remove(row2Delete);
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(change.Reason),
                                $"Reason not handled: {change.Reason}");
                    }
                }

                var rowFilter = $"{Token} = '{baseAsset}' AND {Instrument} NOT IN ({keys.Quoted().ToCsv()})";

                foreach (var row in dataTable.Select(rowFilter))
                {
                    dataTable.Rows.Remove(row);
                }
            }
        }

        observable.Subscribe(OnNext, cancellationToken);

        return dataTable;
    }

    private static object?[] ToArray(this TradeResult tradeResult, Position? position)
    {
        return new object?[]
        {
            tradeResult.Trade.BaseAsset,
            tradeResult.Trade.AssetName,
            position?.Amount ?? 0,
            tradeResult.Trade.Side,
            Math.Abs(tradeResult.Trade.Amount),
            tradeResult.Trade.LimitPrice,
            tradeResult.Trade.Value.Round(2),
            tradeResult.Trade.PostPrice,
            tradeResult.Order?.Created,
            tradeResult.Order?.OrderStatus.ToString(),
            tradeResult.Order?.AmountRemaining
        };
    }

    private static decimal? Round(this decimal? value, int decimals) =>
        value.HasValue ? decimal.Round(value.Value, decimals) : null;
}