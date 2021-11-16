using System.Collections.Generic;
using System.Linq;
using YoloAbstractions;

public static class PositionExtensions
{
    public static decimal GetTotalValue(
        this IDictionary<string, Position> positions,
        IDictionary<string, IEnumerable<Price>> prices,
        string baseCurrencyToken)
    {
        decimal PositionValue(KeyValuePair<string, Position> kvp)
        {
            var (token, (_, assetType, amount)) = kvp;

            if (baseCurrencyToken == token)
            {
                return amount;
            }

            return prices
                .GetPrices(token, baseCurrencyToken, assetType)
                .Select(p => amount * p.Last)
                .FirstOrDefault();
        }

        return positions.Sum(PositionValue);
    }
}