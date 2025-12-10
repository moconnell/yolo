using Moq;
using YoloBroker.Interface;

namespace YoloBroker.Hyperliquid.Test;

internal static class TickerAliasUtil
{
    internal static ITickerAliasService GetTickerAliasService(IReadOnlyDictionary<string, string>? aliases)
    {
        if (aliases != null)
        {
            return new TickerAliasService(aliases);
        }

        var mockTickerAliasService = new Mock<ITickerAliasService>();
#pragma warning disable CS8601 // Possible null reference assignment.
        mockTickerAliasService.Setup(x => x.TryGetAlias(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns(false);
#pragma warning restore CS8601 // Possible null reference assignment.
        return mockTickerAliasService.Object;
    }
}