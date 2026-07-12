using YoloAbstractions;

namespace YoloBroker.Interface;

public interface IUserTradeSource
{
    Task<IReadOnlyCollection<UserTradeRecord>> GetUserTradesByTimeAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);
}
