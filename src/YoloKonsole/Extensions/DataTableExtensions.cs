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

    public static DataTable AsDataTable(
        this IObservable<IChangeSet<KeyValuePair<string, IEnumerable<TradeResult>>, string>> observable,
        CancellationToken cancellationToken)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(Token, typeof(string));
        dataTable.Columns.Add(Instrument, typeof(string));
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
            dataRow[Side] = objects[2];
            dataRow[Amount] = objects[3];
            dataRow[Limit] = objects[4];
            dataRow[Value] = objects[5];
            dataRow[PostOnly] = objects[6];
            dataRow[Created] = objects[7] ?? DBNull.Value;
            dataRow[Status] = objects[8] ?? DBNull.Value;
            dataRow[Remaining] = objects[9] ?? DBNull.Value;
            dataRow.EndEdit();

            return true;
        }

        void OnNext(IChangeSet<KeyValuePair<string, IEnumerable<TradeResult>>, string> changeSet)
        {
            foreach (var change in changeSet)
            {
                var baseAsset = change.Key;
                var tradeResults = change.Current.Value;
                var keys = new List<string>();

                foreach (var result in tradeResults)
                {
                    var key = result.Trade.AssetName;
                    keys.Add(key);

                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                        case ChangeReason.Refresh:
                            var row = dataTable.Rows.Find(key);
                            var itemArray = result.ToArray();
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
                            var row2Delete = dataTable.Rows.Find(key);
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

    private static object?[] ToArray(this TradeResult tradeResult)
    {
        return new object?[]
        {
            tradeResult.Trade.BaseAsset,
            tradeResult.Trade.AssetName,
            tradeResult.Trade.Side,
            Math.Abs(tradeResult.Trade.Amount),
            tradeResult.Trade.LimitPrice,
            tradeResult.Trade.Value.Round(2),
            tradeResult.Trade.PostPrice,
            tradeResult.Order?.Created,
            tradeResult.Order?.OrderStatus,
            tradeResult.Order?.AmountRemaining
        };
    }

    private static decimal? Round(this decimal? value, int decimals) =>
        value.HasValue ? decimal.Round(value.Value, decimals) : null;
}