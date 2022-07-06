using System.Threading.Tasks;
using CryptoExchange.Net;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json.Linq;

namespace YoloRuntime.Test.Mocks;

internal class MockSocketClient : BaseSocketClient
{
    public static readonly MockSocketClient Instance = new();

    private MockSocketClient() : base("", new BaseSocketClientOptions())
    {
    }

    protected override bool HandleQueryResponse<T>(
        SocketConnection socketConnection,
        object request,
        JToken data,
        out CallResult<T>? callResult)
    {
        callResult = null;
        return true;
    }

    protected override bool HandleSubscriptionResponse(
        SocketConnection socketConnection,
        SocketSubscription subscription,
        object request,
        JToken data,
        out CallResult<object>? callResult)
    {
        callResult = null;
        return true;
    }

    protected override bool MessageMatchesHandler(SocketConnection socketConnection, JToken message, object request) =>
        true;

    protected override bool MessageMatchesHandler(
        SocketConnection socketConnection,
        JToken message,
        string identifier) => true;

    protected override Task<CallResult<bool>> AuthenticateSocketAsync(SocketConnection socketConnection) =>
        Task.FromResult(new CallResult<bool>(true));

    protected override Task<bool>
        UnsubscribeAsync(SocketConnection connection, SocketSubscription subscriptionToUnsub) => Task.FromResult(true);
}