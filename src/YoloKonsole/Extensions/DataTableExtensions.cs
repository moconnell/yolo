using System;
using System.Data;
using System.Linq;
using System.Threading;
using DynamicData;
using YoloAbstractions;

namespace YoloKonsole;

public static class DataTableExtensions
{
    private const string FkIdAsset = "FK_Id_Asset";

    public static DataTable AsDataTable(
        this IObservable<IChangeSet<TradeResult, Guid>> observable,
        CancellationToken cancellationToken)
    {
        var linkTable = new DataTable();
        linkTable.Columns.Add("Id", typeof(Guid));
        linkTable.Columns.Add("Asset", typeof(string));
        linkTable.PrimaryKey = new[] { linkTable.Columns[0] };

        var dataTable = new DataTable();
        dataTable.Columns.Add("Asset", typeof(string));
        // dataTable.Columns.Add("Version", typeof(int));
        // dataTable.Columns.Add("Type", typeof(AssetType));
        // dataTable.Columns.Add("BaseAsset", typeof(string));
        dataTable.Columns.Add("Side", typeof(string));
        dataTable.Columns.Add("Amount", typeof(decimal));
        dataTable.Columns.Add("Limit", typeof(decimal));
        dataTable.Columns.Add("Value", typeof(decimal));
        dataTable.Columns.Add("Post-Only", typeof(bool));
        // dataTable.Columns.Add("TradeExpiry", typeof(DateTime));
        // dataTable.Columns.Add("OrderId", typeof(long));
        dataTable.Columns.Add("Created", typeof(DateTime));
        // dataTable.Columns.Add("OrderSide", typeof(OrderSide));
        dataTable.Columns.Add("Status", typeof(OrderStatus));
        // dataTable.Columns.Add("OrderAmount", typeof(decimal));
        dataTable.Columns.Add("Remaining", typeof(decimal));
        // dataTable.Columns.Add("OrderLimitPrice", typeof(decimal));
        // dataTable.Columns.Add("OrderClientId", typeof(string));
        // dataTable.Columns.Add("Success", typeof(bool?));
        // dataTable.Columns.Add("Error", typeof(string));
        // dataTable.Columns.Add("ErrorCode", typeof(int?));

        dataTable.PrimaryKey = new[] { dataTable.Columns[0] };

        var dataSet = new DataSet();
        dataSet.Tables.AddRange(new[] { linkTable, dataTable });
        dataSet.Relations.Add(
            FkIdAsset,
            new[] { linkTable.Columns[1] },
            dataTable.PrimaryKey,
            true);

        void OnNext(IChangeSet<TradeResult, Guid> changeSet)
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                    case ChangeReason.Refresh:
                        var linkRow = linkTable.Rows.Find(change.Key);
                        if (linkRow is { })
                        {
                            var dataRow = linkRow.GetChildRows(FkIdAsset).FirstOrDefault();
                            if (dataRow is { })
                            {
                                var itemArray = change.Current.ToArray();
                                for (var i = 0; i < itemArray.Length; i++)
                                {
                                    dataRow.ItemArray[i] = itemArray[i];
                                }
                            }
                        }
                        else
                        {
                            linkTable.Rows.Add(change.Key, change.Current.Trade.AssetName);
                            dataTable.Rows.Add(change.Current.ToArray());
                        }

                        break;
                    case ChangeReason.Moved:
                        break;
                    case ChangeReason.Remove:
                        var linkRow2Delete = linkTable.Rows.Find(change.Key);
                        if (linkRow2Delete is { })
                        {
                            foreach (var dataRow2Delete in linkRow2Delete.GetChildRows(FkIdAsset))
                                dataTable.Rows.Remove(dataRow2Delete);

                            linkTable.Rows.Remove(linkRow2Delete);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(change.Reason),
                            $"Reason not handled: {change.Reason}");
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
            tradeResult.Trade.AssetName,
            // tradeResult.Trade.AssetType,
            // tradeResult.Trade.BaseAsset,
            tradeResult.Trade.Side,
            Math.Abs(tradeResult.Trade.Amount),
            tradeResult.Trade.LimitPrice,
            tradeResult.Trade.Value.Round(2),
            tradeResult.Trade.PostPrice,
            // tradeResult.Trade.Expiry,
            // tradeResult.Order?.Id,
            tradeResult.Order?.Created,
            // tradeResult.Order?.OrderSide,
            tradeResult.Order?.OrderStatus,
            tradeResult.Order?.AmountRemaining,
            // tradeResult.Order?.LimitPrice,
            // tradeResult.Order?.ClientId,
            // tradeResult.Success,
            // tradeResult.Error,
            // tradeResult.ErrorCode
        };
    }

    private static decimal? Round(this decimal? value, int decimals) =>
        value.HasValue ? decimal.Round(value.Value, decimals) : null;
}