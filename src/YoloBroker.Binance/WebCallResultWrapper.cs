using System.Net;
using CryptoExchange.Net.Objects;

namespace YoloBroker.Binance
{
    public record WebCallResultWrapper(
        bool Success,   
        Error? Error,
        HttpStatusCode? ResponseStatusCode,
        string? ClientOrderId,
        long? OrderId);
}