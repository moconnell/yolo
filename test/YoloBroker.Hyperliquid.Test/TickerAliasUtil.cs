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
        mockTickerAliasService.Setup(x => x.TryGetAlias(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns(false);
        return mockTickerAliasService.Object;
    }
}